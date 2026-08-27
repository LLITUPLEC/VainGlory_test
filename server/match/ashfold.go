package match

import (
	"context"
	"database/sql"
	"encoding/json"

	"github.com/heroiclabs/nakama-common/runtime"
)

// Op-codes клиент ↔ сервер (заготовка боя).
const (
	OpPing      int64 = 1
	OpPong      int64 = 2
	OpChat      int64 = 10
	OpSnapshot  int64 = 20 // позже: состояние кадра
	OpInputMove int64 = 30
	OpInputSkill int64 = 31
)

const (
	tickRate       = 10 // 10 Гц — цель для v1 боя
	emptyTicksMax  = tickRate * 60 // 60 с пустого матча → конец
	maxPlayers     = 6
)

// AshfoldMatch — авторитетный 3v3 (сейчас: join/leave + ping).
type AshfoldMatch struct{}

type playerSlot struct {
	Presence runtime.Presence
	Team     int // 0 Dawn, 1 Dusk
}

type State struct {
	Presences  map[string]*playerSlot
	EmptyTicks int
	Tick       int64
}

func (m *AshfoldMatch) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	state := &State{
		Presences: make(map[string]*playerSlot),
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
	return s, true, ""
}

func (m *AshfoldMatch) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*State)
	s.EmptyTicks = 0
	for _, p := range presences {
		team := len(s.Presences) % 2
		s.Presences[p.GetUserId()] = &playerSlot{Presence: p, Team: team}
		logger.Info("join user=%s team=%d count=%d", p.GetUserId(), team, len(s.Presences))
	}
	payload, _ := json.Marshal(map[string]interface{}{
		"type":  "roster",
		"count": len(s.Presences),
	})
	dispatcher.BroadcastMessage(OpChat, payload, nil, nil, true)
	return s
}

func (m *AshfoldMatch) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*State)
	for _, p := range presences {
		delete(s.Presences, p.GetUserId())
		logger.Info("leave user=%s count=%d", p.GetUserId(), len(s.Presences))
	}
	return s
}

func (m *AshfoldMatch) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {
	s := state.(*State)
	s.Tick = tick

	for _, msg := range messages {
		switch msg.GetOpCode() {
		case OpPing:
			// MatchData сам реализует Presence (nakama-common v1.32)
			dispatcher.BroadcastMessage(OpPong, msg.GetData(), []runtime.Presence{msg}, nil, true)
		default:
			// Пока эхо — позже разбор InputMove / Skill
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
