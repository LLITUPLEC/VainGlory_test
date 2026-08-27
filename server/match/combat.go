package match

import (
	"encoding/json"
	"math"
)

const (
	groundY         = 1.35
	halfLength      = 38.0
	halfWidth       = 22.0
	mapPad          = 2.0
	fountainRadius  = 7.4
	heroBounty      = 120
	dt              = 1.0 / tickRate
	botThinkMod     = 2
	retreatHp       = 0.32
	recoverHp       = 0.85
	arriveEps       = 0.2
)

var botNames = []string{"Rook", "Needle", "Grove", "Ember", "Cinder", "Shade"}

type heroDef struct {
	ID        string
	MaxHP     float64
	Damage    float64
	Range     float64
	Interval  float64
	MoveSpeed float64
	Ranged     bool
	SkillCD    float64
	SkillPower float64
	SkillRange float64
}

type hero struct {
	ID           int
	UserId       string
	Bot          bool
	Team         int
	Slot         int
	HeroId       string
	X, Z, Yaw    float64
	DestX, DestZ float64
	HasMove      bool
	HP, MaxHP    float64
	Damage       float64
	Range        float64
	Interval     float64
	Speed        float64
	Ranged       bool
	AttackCd     float64
	AttackTarget int
	LastSeq      int
	Alive        bool
	RespawnLeft  float64
	Kills        int
	Deaths       int
	Gold         int
	SkillCd      float64
	StunLeft     float64
	Recalling    bool
	RecallLeft   float64
}

type hitEvent struct {
	Src  int     `json:"src"`
	Dst  int     `json:"dst"`
	Dmg  float64 `json:"dmg"`
	Kill int     `json:"kill"`
}

type snapEntity struct {
	ID       int     `json:"id"`
	Kind     string  `json:"kind"`
	UserId   string  `json:"userId"`
	HeroId   string  `json:"heroId"`
	Team     int     `json:"team"`
	Slot     int     `json:"slot"`
	X        float64 `json:"x"`
	Z        float64 `json:"z"`
	Yaw      float64 `json:"yaw"`
	HP       float64 `json:"hp"`
	MaxHP    float64 `json:"maxHp"`
	Respawn  float64 `json:"respawn"`
	Alive    bool    `json:"alive"`
	Bot      bool    `json:"bot"`
	Kills    int     `json:"kills"`
	Deaths   int     `json:"deaths"`
	Gold     int     `json:"gold"`
	TargetId  int     `json:"targetId"`
	AckSeq    int     `json:"ackSeq"`
	StunLeft  float64 `json:"stunLeft"`
	Recalling bool    `json:"recalling"`
	RecallLeft float64 `json:"recallLeft"`
}

type snapDTO struct {
	Type        string       `json:"type"`
	Tick        int64        `json:"tick"`
	Phase       string       `json:"phase"`
	MatchTime   float64      `json:"matchTime"`
	WinnerTeam  int          `json:"winnerTeam"`
	Surrendered bool         `json:"surrendered"`
	Entities    []snapEntity `json:"entities"`
	Hits        []hitEvent   `json:"hits"`
}

var heroDefs = map[string]heroDef{
	"bastion": {ID: "bastion", MaxHP: 820, Damage: 52, Range: 2.3, Interval: 1.05, MoveSpeed: 6.4, Ranged: false, SkillCD: 8, SkillPower: 90, SkillRange: 3.2},
	"vesper":  {ID: "vesper", MaxHP: 470, Damage: 72, Range: 7.2, Interval: 0.85, MoveSpeed: 7.1, Ranged: true, SkillCD: 7, SkillPower: 150, SkillRange: 12},
	"mira":    {ID: "mira", MaxHP: 540, Damage: 38, Range: 5.4, Interval: 1.0, MoveSpeed: 7.0, Ranged: true, SkillCD: 9, SkillPower: 140, SkillRange: 4.5},
}

func resolveHeroId(id string) string {
	if _, ok := heroDefs[id]; ok {
		return id
	}
	return "bastion"
}

func resolveHero(id string) heroDef {
	return heroDefs[resolveHeroId(id)]
}

func botUserId(team, slot int) string {
	return "bot-" + itoa(team) + "-" + itoa(slot)
}

func itoa(n int) string {
	if n < 0 {
		return "0"
	}
	return string(rune('0' + n))
}

func netID(team, slot int) int {
	return team*3 + slot + 1
}

func fountainX(team int) float64 {
	if team == 1 {
		return halfLength
	}
	return -halfLength
}

func spawnHeroes(s *State) {
	s.Heroes = make(map[int]*hero, 6)
	for _, r := range s.Roster {
		if r == nil {
			continue
		}
		def := resolveHero(r.HeroId)
		id := netID(r.Team, r.Slot)
		x := fountainX(r.Team)
		z := float64(r.Slot-1) * 2.0
		s.Heroes[id] = &hero{
			ID:     id,
			UserId: r.UserId,
			Bot:    r.Bot,
			Team:   r.Team,
			Slot:   r.Slot,
			HeroId: resolveHeroId(r.HeroId),
			X:      x,
			Z:      z,
			Yaw:    yawToward(x, z, -x, z),
			HP:     def.MaxHP,
			MaxHP:  def.MaxHP,
			Damage: def.Damage,
			Range:  def.Range,
			Interval: def.Interval,
			Speed:  def.MoveSpeed,
			Ranged: def.Ranged,
			Alive:  true,
			Gold:   80,
		}
	}
}

func applyMoveInput(s *State, userId string, data []byte) {
	h := controllable(s, userId)
	if h == nil {
		return
	}
	var dto struct {
		X   float64 `json:"x"`
		Z   float64 `json:"z"`
		Seq int     `json:"seq"`
	}
	if json.Unmarshal(data, &dto) != nil {
		return
	}
	if staleSeq(h, dto.Seq) {
		return
	}
	cancelRecall(h)
	h.AttackTarget = 0
	setDest(h, dto.X, dto.Z)
}

func applyAttackInput(s *State, userId string, data []byte) {
	h := controllable(s, userId)
	if h == nil {
		return
	}
	var dto struct {
		TargetId int `json:"targetId"`
		Seq      int `json:"seq"`
	}
	if json.Unmarshal(data, &dto) != nil {
		return
	}
	if staleSeq(h, dto.Seq) {
		return
	}
	cancelRecall(h)
	t := s.Heroes[dto.TargetId]
	if t == nil {
		ex := s.Extras[dto.TargetId]
		if ex == nil || !ex.Alive || ex.Team == h.Team {
			return
		}
		h.AttackTarget = dto.TargetId
		h.HasMove = false
		return
	}
	if !t.Alive || t.Team == h.Team {
		return
	}
	h.AttackTarget = dto.TargetId
	h.HasMove = false
}

func applyStopInput(s *State, userId string) {
	h := controllable(s, userId)
	if h == nil {
		return
	}
	h.AttackTarget = 0
	h.HasMove = false
}

func applyRecallInput(s *State, userId string, data []byte) {
	h := controllable(s, userId)
	if h == nil {
		return
	}
	var dto struct {
		Seq int `json:"seq"`
	}
	_ = json.Unmarshal(data, &dto)
	if staleSeq(h, dto.Seq) {
		return
	}
	if inFountain(h) {
		return
	}
	h.Recalling = true
	h.RecallLeft = 2.5
	h.AttackTarget = 0
	h.HasMove = false
}

func applySkillInput(s *State, userId string, data []byte) {
	h := controllable(s, userId)
	if h == nil || h.StunLeft > 0 {
		return
	}
	var dto struct {
		Yaw float64 `json:"yaw"`
		Seq int     `json:"seq"`
	}
	if json.Unmarshal(data, &dto) != nil {
		return
	}
	if staleSeq(h, dto.Seq) {
		return
	}
	if h.SkillCd > 0 {
		return
	}
	def := resolveHero(h.HeroId)
	cancelRecall(h)
	h.SkillCd = def.SkillCD
	h.HasMove = false
	h.AttackTarget = 0
	h.Yaw = dto.Yaw
	switch h.HeroId {
	case "vesper":
		castBolt(s, h, def)
	case "mira":
		castNova(s, h, def)
	default:
		castCone(s, h, def)
	}
}

func cancelRecall(h *hero) {
	if h == nil {
		return
	}
	h.Recalling = false
	h.RecallLeft = 0
}

func castCone(s *State, h *hero, def heroDef) {
	for _, t := range s.Heroes {
		if t == nil || t == h || !t.Alive || t.Team == h.Team {
			continue
		}
		if !inSkillArc(h, t.X, t.Z, def.SkillRange, 55) {
			continue
		}
		hurt(s, h.ID, h.Team, true, def.SkillPower, t.ID)
		t.StunLeft = math.Max(t.StunLeft, 0.85)
	}
	for _, u := range s.Extras {
		if u == nil || !u.Alive || u.Team == h.Team {
			continue
		}
		if !inSkillArc(h, u.X, u.Z, def.SkillRange, 55) {
			continue
		}
		hurt(s, h.ID, h.Team, true, def.SkillPower, u.ID)
	}
}

func castBolt(s *State, h *hero, def heroDef) {
	best := 0
	bestD := def.SkillRange
	for _, t := range s.Heroes {
		if t == nil || t == h || !t.Alive || t.Team == h.Team {
			continue
		}
		if d, ok := alongRay(h, t.X, t.Z, def.SkillRange, 0.9); ok && d < bestD {
			bestD = d
			best = t.ID
		}
	}
	for _, u := range s.Extras {
		if u == nil || !u.Alive || u.Team == h.Team {
			continue
		}
		if d, ok := alongRay(h, u.X, u.Z, def.SkillRange, 0.9); ok && d < bestD {
			bestD = d
			best = u.ID
		}
	}
	if best != 0 {
		hurt(s, h.ID, h.Team, true, def.SkillPower, best)
	}
}

func castNova(s *State, h *hero, def heroDef) {
	for _, t := range s.Heroes {
		if t == nil || !t.Alive {
			continue
		}
		if dist(h.X, h.Z, t.X, t.Z) > def.SkillRange {
			continue
		}
		if t.Team == h.Team {
			t.HP = math.Min(t.MaxHP, t.HP+def.SkillPower)
			continue
		}
		hurt(s, h.ID, h.Team, true, def.SkillPower*0.55, t.ID)
	}
	for _, u := range s.Extras {
		if u == nil || !u.Alive || u.Team == h.Team {
			continue
		}
		if dist(h.X, h.Z, u.X, u.Z) > def.SkillRange {
			continue
		}
		hurt(s, h.ID, h.Team, true, def.SkillPower*0.55, u.ID)
	}
}

func inSkillArc(h *hero, x, z, rng, halfAngle float64) bool {
	d := dist(h.X, h.Z, x, z)
	if d > rng || d < 0.01 {
		return d <= rng && d >= 0
	}
	return angleDiff(h.Yaw, yawToward(h.X, h.Z, x, z)) <= halfAngle
}

func alongRay(h *hero, x, z, maxDist, halfW float64) (float64, bool) {
	rad := h.Yaw * math.Pi / 180
	fx := math.Sin(rad)
	fz := math.Cos(rad)
	dx := x - h.X
	dz := z - h.Z
	forward := dx*fx + dz*fz
	if forward < 0 || forward > maxDist {
		return 0, false
	}
	lat := math.Abs(dx*fz - dz*fx)
	if lat > halfW {
		return 0, false
	}
	return forward, true
}

func angleDiff(a, b float64) float64 {
	d := math.Mod(math.Abs(a-b), 360)
	if d > 180 {
		d = 360 - d
	}
	return d
}

func staleSeq(h *hero, seq int) bool {
	if seq == 0 {
		return false
	}
	if seq <= h.LastSeq {
		return true
	}
	h.LastSeq = seq
	return false
}

func controllable(s *State, userId string) *hero {
	if s.Phase != phaseCombat {
		return nil
	}
	h := heroByUser(s, userId)
	if h == nil || !h.Alive || h.Bot {
		return nil
	}
	return h
}

func heroByUser(s *State, userId string) *hero {
	for _, h := range s.Heroes {
		if h != nil && h.UserId == userId {
			return h
		}
	}
	return nil
}

func setDest(h *hero, x, z float64) {
	h.DestX = clamp(x, -halfLength-mapPad, halfLength+mapPad)
	h.DestZ = clamp(z, -halfWidth, halfWidth)
	h.HasMove = true
}

func tickCombat(s *State) {
	s.MatchTimeTicks++
	if s.Tick%botThinkMod == 0 {
		thinkBots(s)
	}
	for _, h := range s.Heroes {
		stepHero(s, h)
	}
	tickWorld(s)
	checkEnd(s)
}

func thinkBots(s *State) {
	for _, h := range s.Heroes {
		if h == nil || !h.Bot || !h.Alive {
			continue
		}
		ratio := h.HP / h.MaxHP
		if inFountain(h) && ratio < recoverHp {
			h.AttackTarget = 0
			h.HasMove = false
			continue
		}
		if ratio < retreatHp && !inFountain(h) {
			h.AttackTarget = 0
			setDest(h, fountainX(h.Team), 0)
			continue
		}
		enemy := nearestHostile(s, h.X, h.Z, h.Team, 80, false)
		if enemy == 0 {
			setDest(h, -fountainX(h.Team), 0)
			h.AttackTarget = 0
			continue
		}
		h.AttackTarget = enemy
		h.HasMove = false
	}
}

func stepHero(s *State, h *hero) {
	if h == nil {
		return
	}
	if !h.Alive {
		h.RespawnLeft -= dt
		if h.RespawnLeft <= 0 {
			h.Alive = true
			h.HP = h.MaxHP
			h.X = fountainX(h.Team)
			h.Z = float64(h.Slot-1) * 2.0
			h.AttackTarget = 0
			h.HasMove = false
			h.AttackCd = 0
			h.SkillCd = 0
			h.StunLeft = 0
			cancelRecall(h)
		}
		return
	}

	if h.AttackCd > 0 {
		h.AttackCd -= dt
	}
	if h.SkillCd > 0 {
		h.SkillCd -= dt
	}
	if h.StunLeft > 0 {
		h.StunLeft -= dt
		if h.StunLeft < 0 {
			h.StunLeft = 0
		}
	}
	if inFountain(h) {
		h.HP = math.Min(h.MaxHP, h.HP+h.MaxHP*0.22*dt)
	}

	if h.StunLeft > 0 {
		h.HasMove = false
		h.AttackTarget = 0
		cancelRecall(h)
		return
	}

	if h.Recalling {
		h.HasMove = false
		h.AttackTarget = 0
		h.RecallLeft -= dt
		if h.RecallLeft <= 0 {
			cancelRecall(h)
			h.X = fountainX(h.Team)
			h.Z = float64(h.Slot-1) * 2.0
			h.HP = math.Min(h.MaxHP, h.HP+h.MaxHP*0.15)
		}
		return
	}

	if h.AttackTarget != 0 {
		x, z, team, ok := liveXZ(s, h.AttackTarget)
		if !ok || team == h.Team {
			h.AttackTarget = 0
		} else {
			d := dist(h.X, h.Z, x, z)
			h.Yaw = yawToward(h.X, h.Z, x, z)
			if d > h.Range {
				moveToward(h, x, z)
			} else {
				h.HasMove = false
				if h.AttackCd <= 0 {
					h.AttackCd = h.Interval
					hurt(s, h.ID, h.Team, true, h.Damage, h.AttackTarget)
				}
			}
			return
		}
	}

	if h.HasMove {
		moveToward(h, h.DestX, h.DestZ)
	}
}

func respawnSeconds(s *State) float64 {
	minutes := math.Floor(float64(s.MatchTimeTicks) * dt / 60.0)
	return 5.0 + 2.0*minutes
}

func moveToward(h *hero, x, z float64) {
	dx := x - h.X
	dz := z - h.Z
	d := math.Hypot(dx, dz)
	if d <= arriveEps {
		h.X = clamp(x, -halfLength-mapPad, halfLength+mapPad)
		h.Z = clamp(z, -halfWidth, halfWidth)
		h.HasMove = false
		return
	}
	step := h.Speed * dt
	if step >= d {
		h.X = clamp(x, -halfLength-mapPad, halfLength+mapPad)
		h.Z = clamp(z, -halfWidth, halfWidth)
		h.HasMove = false
	} else {
		h.X = clamp(h.X+dx/d*step, -halfLength-mapPad, halfLength+mapPad)
		h.Z = clamp(h.Z+dz/d*step, -halfWidth, halfWidth)
	}
	h.Yaw = yawToward(h.X-dx, h.Z-dz, x, z)
}

func inFountain(h *hero) bool {
	return dist(h.X, h.Z, fountainX(h.Team), 0) <= fountainRadius
}

func checkEnd(s *State) {
	if s.Phase != phaseCombat {
		return
	}
	timeSec := float64(s.MatchTimeTicks) * dt
	if timeSec < matchTimeLimit {
		return
	}
	k0, k1 := 0, 0
	for _, h := range s.Heroes {
		if h == nil {
			continue
		}
		if h.Team == 0 {
			k0 += h.Kills
		} else {
			k1 += h.Kills
		}
	}
	winner := 0
	if k1 > k0 {
		winner = 1
	}
	endMatch(s, winner, false)
}

func endMatch(s *State, winnerTeam int, surrendered bool) {
	if s.Phase == phaseEnded {
		return
	}
	s.Phase = phaseEnded
	s.WinnerTeam = winnerTeam
	s.Surrendered = surrendered
	s.EndedTicks = 0
}

func snapshotPayload(s *State) []byte {
	ents := make([]snapEntity, 0, len(s.Heroes))
	for id := 1; id <= 6; id++ {
		h := s.Heroes[id]
		if h == nil {
			continue
		}
		ents = append(ents, snapEntity{
			ID:       h.ID,
			Kind:     "hero",
			UserId:   h.UserId,
			HeroId:   h.HeroId,
			Team:     h.Team,
			Slot:     h.Slot,
			X:        h.X,
			Z:        h.Z,
			Yaw:      h.Yaw,
			HP:       h.HP,
			MaxHP:    h.MaxHP,
			Respawn:  h.RespawnLeft,
			Alive:    h.Alive,
			Bot:      h.Bot,
			Kills:    h.Kills,
			Deaths:   h.Deaths,
			Gold:     h.Gold,
			TargetId:  h.AttackTarget,
			AckSeq:    h.LastSeq,
			StunLeft:  h.StunLeft,
			Recalling: h.Recalling,
			RecallLeft: h.RecallLeft,
		})
	}
	ents = extraSnapshot(s, ents)
	hits := s.Hits
	if hits == nil {
		hits = []hitEvent{}
	}
	winner := s.WinnerTeam
	if winner < 0 {
		winner = 0
	}
	b, _ := json.Marshal(snapDTO{
		Type:        "snap",
		Tick:        s.Tick,
		Phase:       s.Phase,
		MatchTime:   float64(s.MatchTimeTicks) * dt,
		WinnerTeam:  winner,
		Surrendered: s.Surrendered,
		Entities:    ents,
		Hits:        hits,
	})
	return b
}

func dist(x1, z1, x2, z2 float64) float64 {
	return math.Hypot(x2-x1, z2-z1)
}

func yawToward(x, z, tx, tz float64) float64 {
	dx := tx - x
	dz := tz - z
	if dx*dx+dz*dz < 0.0001 {
		return 0
	}
	return math.Atan2(dx, dz) * 180.0 / math.Pi
}

func clamp(v, lo, hi float64) float64 {
	if v < lo {
		return lo
	}
	if v > hi {
		return hi
	}
	return v
}
