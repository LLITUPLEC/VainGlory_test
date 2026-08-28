package match

import (
	"context"
	"database/sql"
	"encoding/json"

	"github.com/heroiclabs/nakama-common/runtime"
)

const (
	OpPing       int64 = 1
	OpPong       int64 = 2
	OpRoster     int64 = 11
	OpSnapshot   int64 = 20
	OpInputMove  int64 = 30
	OpInputSkill int64 = 31
	OpDraftPick  int64 = 32
	OpDraftLock  int64 = 33
	OpInputAttack int64 = 34
	OpInputStop  int64 = 35
	OpInputRecall int64 = 36
	OpInputBuy    int64 = 37
	OpSurrender  int64 = 40
	OpMapPing    int64 = 50
)

const (
	tickRate         = 10
	emptyTicksMax    = tickRate * 60
	endedTicksMax    = tickRate * 45
	maxPlayers       = 6
	humansToStart    = 2
	draftTicks       = 20 * tickRate
	loadingTicks     = 4 * tickRate
	reconnectTicks   = 30 * tickRate
	matchTimeLimit   = 10 * 60
	phaseWaiting     = "waiting"
	phaseDraft       = "draft"
	phaseLoading     = "loading"
	phaseCombat      = "combat"
	phaseEnded       = "ended"
)

type AshfoldMatch struct{}

type playerSlot struct {
	Presence runtime.Presence
	Name     string
	Team     int
	Slot     int
	Party    string
}

type rosterSlot struct {
	UserId string
	Name   string
	Team   int
	Slot   int
	HeroId string
	Locked bool
	Bot    bool
	Away   int
	LastPing int64
}

type State struct {
	Presences        map[string]*playerSlot
	PendingNames     map[string]string
	PendingParty     map[string]string
	Roster           [6]*rosterSlot
	Heroes           map[int]*hero
	Extras           map[int]*ent
	Hits             []hitEvent
	NextMinionId     int
	WaveTicks        int
	Phase            string
	Debug            bool
	EmptyTicks       int
	EndedTicks       int
	Tick             int64
	DraftLeftTicks   int
	LoadingLeftTicks int
	MatchTimeTicks   int64
	WinnerTeam       int
	Surrendered      bool
	DraftStarted     bool
	CombatStarted    bool
	BotsFilled       bool
}

func (m *AshfoldMatch) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	state := &State{
		Presences:    make(map[string]*playerSlot),
		PendingNames: make(map[string]string),
		PendingParty: make(map[string]string),
		Heroes:       make(map[int]*hero),
		Extras:       make(map[int]*ent),
		Hits:         make([]hitEvent, 0, 8),
		NextMinionId: 100,
		Phase:        phaseWaiting,
		Debug:        params["debug"] == true,
		WinnerTeam:   -1,
	}
	labelBytes, _ := json.Marshal(map[string]interface{}{
		"mode":  "casual_3v3",
		"open":  true,
		"debug": state.Debug,
	})
	logger.Info("Ashfold match init tickRate=%d debug=%v", tickRate, state.Debug)
	return state, tickRate, string(labelBytes)
}

func (m *AshfoldMatch) MatchJoinAttempt(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presence runtime.Presence, metadata map[string]string) (interface{}, bool, string) {
	s := state.(*State)
	if s.Phase == phaseEnded {
		return s, false, "match ended"
	}
	if _, already := s.Presences[presence.GetUserId()]; already {
		return s, true, ""
	}
	if slot := findRosterByUser(s, presence.GetUserId()); slot != nil && !isBotUser(slot.UserId) {
		return s, true, ""
	}
	if s.Phase != phaseWaiting {
		return s, false, "already started"
	}
	if humanCount(s) >= maxPlayers {
		return s, false, "match full"
	}
	if name := metadata["name"]; name != "" {
		s.PendingNames[presence.GetUserId()] = name
	}
	if pid := metadata["party"]; pid != "" {
		s.PendingParty[presence.GetUserId()] = pid
	}
	return s, true, ""
}

func (m *AshfoldMatch) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*State)
	s.EmptyTicks = 0
	for _, p := range presences {
		if existing := s.Presences[p.GetUserId()]; existing != nil {
			existing.Presence = p
			continue
		}
		if slot := findRosterByUser(s, p.GetUserId()); slot != nil && !isBotUser(slot.UserId) {
			slot.Bot = false
			slot.Away = 0
			s.Presences[p.GetUserId()] = &playerSlot{Presence: p, Name: slot.Name, Team: slot.Team, Slot: slot.Slot}
			if h := heroByUser(s, p.GetUserId()); h != nil {
				h.Bot = false
				h.LastSeq = 0
			}
			logger.Info("rejoin user=%s team=%d slot=%d", p.GetUserId(), slot.Team, slot.Slot)
			continue
		}
		team := 0
		partyId := s.PendingParty[p.GetUserId()]
		delete(s.PendingParty, p.GetUserId())
		if partyId != "" {
			if t, ok := teamForParty(s, partyId); ok {
				team = t
			} else if teamCount(s, 0) >= 3 {
				team = 1
			}
		} else if teamCount(s, 0) > teamCount(s, 1) {
			team = 1
		}
		slot := nextSlot(s, team)
		name := s.PendingNames[p.GetUserId()]
		if name == "" {
			name = p.GetUsername()
		}
		delete(s.PendingNames, p.GetUserId())
		s.Presences[p.GetUserId()] = &playerSlot{Presence: p, Name: name, Team: team, Slot: slot, Party: partyId}
		logger.Info("join user=%s name=%s team=%d slot=%d count=%d", p.GetUserId(), name, team, slot, len(s.Presences))
	}
	dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
	return s
}

func (m *AshfoldMatch) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*State)
	for _, p := range presences {
		delete(s.Presences, p.GetUserId())
		if slot := findRosterByUser(s, p.GetUserId()); slot != nil && !isBotUser(slot.UserId) {
			slot.Away = 1
			logger.Info("leave user=%s grace=%ds count=%d", p.GetUserId(), reconnectTicks/tickRate, len(s.Presences))
		} else {
			logger.Info("leave user=%s count=%d", p.GetUserId(), len(s.Presences))
		}
	}
	if len(s.Presences) > 0 && s.Phase != phaseEnded {
		dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
	}
	return s
}

func (m *AshfoldMatch) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {
	s := state.(*State)
	s.Tick = tick
	s.Hits = s.Hits[:0]
	tickAway(s, logger)

	for _, msg := range messages {
		switch msg.GetOpCode() {
		case OpPing:
			dispatcher.BroadcastMessage(OpPong, msg.GetData(), []runtime.Presence{msg}, nil, true)
		case OpDraftPick:
			applyPick(s, msg.GetUserId(), msg.GetData(), false)
			dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
		case OpDraftLock:
			applyPick(s, msg.GetUserId(), msg.GetData(), true)
			dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
		case OpInputMove:
			applyMoveInput(s, msg.GetUserId(), msg.GetData())
		case OpInputAttack:
			applyAttackInput(s, msg.GetUserId(), msg.GetData())
		case OpInputSkill:
			applySkillInput(s, msg.GetUserId(), msg.GetData())
		case OpInputStop:
			applyStopInput(s, msg.GetUserId())
		case OpInputRecall:
			applyRecallInput(s, msg.GetUserId(), msg.GetData())
		case OpInputBuy:
			applyBuyInput(s, msg.GetUserId(), msg.GetData())
		case OpSurrender:
			if applySurrender(s, msg.GetUserId()) {
				dispatcher.BroadcastMessage(OpSnapshot, snapshotPayload(s), nil, nil, true)
			}
		case OpMapPing:
			applyMapPing(s, msg.GetUserId(), msg.GetData(), dispatcher)
		}
	}

	switch s.Phase {
	case phaseWaiting:
		need := humansToStart
		if s.Debug {
			need = 1
		}
		if humanCount(s) >= need {
			startDraft(s, logger)
			dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
		}
	case phaseDraft:
		if tickDraft(s, logger) {
			dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
		} else if tick%tickRate == 0 {
			dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
		}
	case phaseLoading:
		if tickLoading(s, logger) {
			dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
		}
		dispatcher.BroadcastMessage(OpSnapshot, snapshotPayload(s), nil, nil, true)
	case phaseCombat:
		tickCombat(s)
		dispatcher.BroadcastMessage(OpSnapshot, snapshotPayload(s), nil, nil, true)
		if s.Phase == phaseEnded {
			dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
		}
	}

	if s.Phase == phaseEnded {
		s.EndedTicks++
		if s.EndedTicks >= endedTicksMax {
			logger.Info("ended match close tick=%d", tick)
			return nil
		}
		return s
	}

	if len(s.Presences) == 0 {
		s.EmptyTicks++
		if s.EmptyTicks >= emptyTicksMax {
			logger.Info("empty match end tick=%d", tick)
			return nil
		}
	} else {
		s.EmptyTicks = 0
	}

	return s
}

func (m *AshfoldMatch) MatchTerminate(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, graceSeconds int) interface{} {
	logger.Info("match terminate grace=%d", graceSeconds)
	return state
}

func (m *AshfoldMatch) MatchSignal(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, data string) (interface{}, string) {
	return state, "ok:" + data
}

func startDraft(s *State, logger runtime.Logger) {
	if s.DraftStarted {
		return
	}
	s.DraftStarted = true
	s.Phase = phaseDraft
	s.DraftLeftTicks = draftTicks
	fillRoster(s)
	logger.Info("draft start humans=%d", humanCount(s))
}

func tickDraft(s *State, logger runtime.Logger) bool {
	s.DraftLeftTicks--
	elapsed := draftTicks - s.DraftLeftTicks
	lockBotsAtElapsed(s, elapsed)

	if s.DraftLeftTicks > 0 && !allHumansLocked(s) {
		return false
	}
	lockRemaining(s)
	startLoading(s, logger)
	return true
}

func startLoading(s *State, logger runtime.Logger) {
	if s.CombatStarted {
		return
	}
	s.Phase = phaseLoading
	s.LoadingLeftTicks = loadingTicks
	spawnHeroes(s)
	spawnWorld(s)
	s.CombatStarted = true
	logger.Info("loading start heroes=%d extras=%d", len(s.Heroes), len(s.Extras))
}

func tickLoading(s *State, logger runtime.Logger) bool {
	s.LoadingLeftTicks--
	if s.LoadingLeftTicks > 0 {
		return false
	}
	s.Phase = phaseCombat
	s.WaveTicks = 20
	logger.Info("combat start")
	return true
}

func fillRoster(s *State) {
	if s.BotsFilled {
		return
	}
	s.BotsFilled = true
	for _, p := range s.Presences {
		idx := p.Team*3 + p.Slot
		if idx < 0 || idx >= 6 {
			continue
		}
		s.Roster[idx] = &rosterSlot{
			UserId: p.Presence.GetUserId(),
			Name:   p.Name,
			Team:   p.Team,
			Slot:   p.Slot,
		}
	}
	bot := 0
	for team := 0; team < 2; team++ {
		for slot := 0; slot < 3; slot++ {
			idx := team*3 + slot
			if s.Roster[idx] != nil {
				continue
			}
			s.Roster[idx] = &rosterSlot{
				UserId: botUserId(team, slot),
				Name:   botNames[bot%len(botNames)],
				Team:   team,
				Slot:   slot,
				Bot:    true,
			}
			bot++
		}
	}
}

func lockBotsAtElapsed(s *State, elapsed int) {
	times := []int{40, 70, 100, 130, 160}
	n := 0
	for _, t := range times {
		if elapsed >= t {
			n++
		}
	}
	locked := 0
	for _, r := range s.Roster {
		if r != nil && r.Bot && r.Locked {
			locked++
		}
	}
	for locked < n {
		if !lockNextBot(s, locked) {
			return
		}
		locked++
	}
}

func lockNextBot(s *State, index int) bool {
	ids := []string{"bastion", "vesper", "mira"}
	for _, r := range s.Roster {
		if r == nil || !r.Bot || r.Locked {
			continue
		}
		r.HeroId = ids[index%len(ids)]
		r.Locked = true
		return true
	}
	return false
}

func lockRemaining(s *State) {
	ids := []string{"bastion", "vesper", "mira"}
	botIndex := 0
	for _, r := range s.Roster {
		if r == nil || r.Locked {
			if r != nil && r.Bot {
				botIndex++
			}
			continue
		}
		if r.HeroId == "" {
			if r.Bot {
				r.HeroId = ids[botIndex%len(ids)]
			} else {
				r.HeroId = "bastion"
			}
		}
		r.Locked = true
		if r.Bot {
			botIndex++
		}
	}
}

func allHumansLocked(s *State) bool {
	any := false
	for _, r := range s.Roster {
		if r == nil || r.Bot {
			continue
		}
		any = true
		if !r.Locked {
			return false
		}
	}
	return any
}

func applyPick(s *State, userId string, data []byte, lock bool) {
	if s.Phase != phaseDraft {
		return
	}
	var dto struct {
		HeroId string `json:"heroId"`
	}
	_ = json.Unmarshal(data, &dto)
	r := findRosterByUser(s, userId)
	if r == nil || r.Bot || r.Locked {
		return
	}
	r.HeroId = resolveHeroId(dto.HeroId)
	if lock {
		r.Locked = true
	}
}

func applySurrender(s *State, userId string) bool {
	if s.Phase != phaseCombat && s.Phase != phaseLoading {
		return false
	}
	r := findRosterByUser(s, userId)
	if r == nil || r.Bot {
		return false
	}
	endMatch(s, 1-r.Team, true)
	return true
}

func applyMapPing(s *State, userId string, data []byte, dispatcher runtime.MatchDispatcher) {
	if s.Phase != phaseCombat && s.Phase != phaseLoading && s.Phase != phaseDraft {
		return
	}
	r := findRosterByUser(s, userId)
	if r == nil || r.Bot {
		return
	}
	if r.LastPing != 0 && s.Tick-r.LastPing < int64(tickRate*2) {
		return
	}
	var dto struct {
		X float64 `json:"x"`
		Z float64 `json:"z"`
	}
	if json.Unmarshal(data, &dto) != nil {
		return
	}
	r.LastPing = s.Tick
	dto.X = clamp(dto.X, -halfLength-mapPad, halfLength+mapPad)
	dto.Z = clamp(dto.Z, -halfWidth, halfWidth)
	payload, err := json.Marshal(map[string]interface{}{
		"x":      dto.X,
		"z":      dto.Z,
		"team":   r.Team,
		"userId": r.UserId,
		"name":   r.Name,
	})
	if err != nil {
		return
	}
	targets := make([]runtime.Presence, 0, 3)
	for _, p := range s.Presences {
		if p == nil || p.Presence == nil {
			continue
		}
		slot := findRosterByUser(s, p.Presence.GetUserId())
		if slot != nil && slot.Team == r.Team {
			targets = append(targets, p.Presence)
		}
	}
	if len(targets) == 0 {
		return
	}
	dispatcher.BroadcastMessage(OpMapPing, payload, targets, nil, true)
}

func humanCount(s *State) int {
	return len(s.Presences)
}

func isBotUser(userId string) bool {
	return len(userId) >= 4 && userId[:4] == "bot-"
}

func tickAway(s *State, logger runtime.Logger) {
	if s.Phase == phaseEnded || s.Phase == phaseWaiting {
		return
	}
	for _, r := range s.Roster {
		if r == nil || isBotUser(r.UserId) {
			continue
		}
		if _, online := s.Presences[r.UserId]; online {
			r.Away = 0
			continue
		}
		if r.Away <= 0 {
			continue
		}
		r.Away++
		if r.Away == reconnectTicks {
			r.Bot = true
			if h := heroByUser(s, r.UserId); h != nil {
				h.Bot = true
			}
			logger.Info("away timeout user=%s → bot", r.UserId)
		}
	}
}

func teamCount(s *State, team int) int {
	n := 0
	for _, p := range s.Presences {
		if p.Team == team {
			n++
		}
	}
	return n
}

func teamForParty(s *State, partyId string) (int, bool) {
	if partyId == "" {
		return 0, false
	}
	for _, p := range s.Presences {
		if p != nil && p.Party == partyId {
			return p.Team, true
		}
	}
	return 0, false
}

func nextSlot(s *State, team int) int {
	used := [3]bool{}
	for _, p := range s.Presences {
		if p.Team == team && p.Slot >= 0 && p.Slot < 3 {
			used[p.Slot] = true
		}
	}
	for i := 0; i < 3; i++ {
		if !used[i] {
			return i
		}
	}
	return 0
}

func findRosterByUser(s *State, userId string) *rosterSlot {
	for _, r := range s.Roster {
		if r != nil && r.UserId == userId {
			return r
		}
	}
	return nil
}

func rosterPayload(s *State) []byte {
	players := make([]map[string]interface{}, 0, 6)
	if s.BotsFilled {
		for _, r := range s.Roster {
			if r == nil {
				continue
			}
			players = append(players, map[string]interface{}{
				"userId":   r.UserId,
				"username": r.Name,
				"team":     r.Team,
				"slot":     r.Slot,
				"heroId":   r.HeroId,
				"locked":   r.Locked,
				"bot":      r.Bot,
			})
		}
	} else {
		for id, p := range s.Presences {
			players = append(players, map[string]interface{}{
				"userId":   id,
				"username": p.Name,
				"team":     p.Team,
				"slot":     p.Slot,
				"heroId":   "",
				"locked":   false,
				"bot":      false,
			})
		}
	}
	draftLeft := 0.0
	if s.Phase == phaseDraft && s.DraftLeftTicks > 0 {
		draftLeft = float64(s.DraftLeftTicks) / float64(tickRate)
	}
	b, _ := json.Marshal(map[string]interface{}{
		"type":      "roster",
		"phase":     s.Phase,
		"draftLeft": draftLeft,
		"count":     len(players),
		"players":   players,
	})
	return b
}
