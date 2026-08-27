package match

import (
	"context"
	"database/sql"
	"encoding/json"

	"github.com/heroiclabs/nakama-common/runtime"
)

// Op-codes клиент ↔ сервер (заготовка боя).
const (
	OpPing       int64 = 1
	OpPong       int64 = 2
	OpChat       int64 = 10
	OpRoster     int64 = 11
	OpSnapshot   int64 = 20
	OpInputMove  int64 = 30
	OpInputSkill int64 = 31
)

const (
	tickRate      = 10
	emptyTicksMax = tickRate * 60
	maxPlayers    = 6
)

type AshfoldMatch struct{}

type playerSlot struct {
	Presence runtime.Presence
	Name     string
	Team     int // 0 Dawn, 1 Dusk
	Slot     int // 0..2
}

type State struct {
	Presences    map[string]*playerSlot
	PendingNames map[string]string
	EmptyTicks   int
	Tick         int64
}

func (m *AshfoldMatch) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	state := &State{
		Presences:    make(map[string]*playerSlot),
		PendingNames: make(map[string]string),
	}
	labelBytes, _ := json.Marshal(map[string]interface{}{
		"mode":  "casual_3v3",
		"open":  true,
		"debug": params["debug"] == true,
	})
	logger.Info("Ashfold match init tickRate=%d", tickRate)
	return state, tickRate, string(labelBytes)
}

func (m *AshfoldMatch) MatchJoinAttempt(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presence runtime.Presence, metadata map[string]string) (interface{}, bool, string) {
	s := state.(*State)
	if len(s.Presences) >= maxPlayers {
		return s, false, "match full"
	}
	if name := metadata["name"]; name != "" {
		s.PendingNames[presence.GetUserId()] = name
	}
	return s, true, ""
}

func (m *AshfoldMatch) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*State)
	s.EmptyTicks = 0
	for _, p := range presences {
		team := 0
		if teamCount(s, 0) > teamCount(s, 1) {
			team = 1
		}
		slot := nextSlot(s, team)
		name := s.PendingNames[p.GetUserId()]
		if name == "" {
			name = p.GetUsername()
		}
		delete(s.PendingNames, p.GetUserId())
		s.Presences[p.GetUserId()] = &playerSlot{Presence: p, Name: name, Team: team, Slot: slot}
		logger.Info("join user=%s name=%s team=%d slot=%d count=%d", p.GetUserId(), name, team, slot, len(s.Presences))
	}
	dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
	return s
}

func (m *AshfoldMatch) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*State)
	for _, p := range presences {
		delete(s.Presences, p.GetUserId())
		logger.Info("leave user=%s count=%d", p.GetUserId(), len(s.Presences))
	}
	if len(s.Presences) > 0 {
		dispatcher.BroadcastMessage(OpRoster, rosterPayload(s), nil, nil, true)
	}
	return s
}

func (m *AshfoldMatch) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {
	s := state.(*State)
	s.Tick = tick

	for _, msg := range messages {
		switch msg.GetOpCode() {
		case OpPing:
			dispatcher.BroadcastMessage(OpPong, msg.GetData(), []runtime.Presence{msg}, nil, true)
		default:
			dispatcher.BroadcastMessage(msg.GetOpCode(), msg.GetData(), nil, nil, true)
		}
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

func teamCount(s *State, team int) int {
	n := 0
	for _, p := range s.Presences {
		if p.Team == team {
			n++
		}
	}
	return n
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

func rosterPayload(s *State) []byte {
	players := make([]map[string]interface{}, 0, len(s.Presences))
	for id, p := range s.Presences {
		players = append(players, map[string]interface{}{
			"userId":   id,
			"username": p.Name,
			"team":     p.Team,
			"slot":     p.Slot,
		})
	}
	b, _ := json.Marshal(map[string]interface{}{
		"type":    "roster",
		"count":   len(s.Presences),
		"players": players,
	})
	return b
}
