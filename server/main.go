package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"time"

	"github.com/ashfold/server/match"
	"github.com/heroiclabs/nakama-common/runtime"
)

const (
	rpcHealth   = "ashfold_health"
	rpcCreateDebugMatch = "ashfold_create_debug_match"
	matchName   = "ashfold_3v3"
)

// InitModule — точка входа Go-плагина Nakama.
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	logger.Info("Ashfold backend init")

	if err := initializer.RegisterMatch(matchName, func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule) (runtime.Match, error) {
		return &match.AshfoldMatch{}, nil
	}); err != nil {
		return err
	}

	if err := initializer.RegisterRpc(rpcHealth, rpcHealthHandler); err != nil {
		return err
	}

	if err := initializer.RegisterRpc(rpcCreateDebugMatch, rpcCreateDebugMatchHandler); err != nil {
		return err
	}

	if err := initializer.RegisterMatchmakerMatched(func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, entries []runtime.MatchmakerEntry) (string, error) {
		logger.Info("Ashfold matchmaker matched count=%d", len(entries))
		matchID, err := nk.MatchCreate(ctx, matchName, map[string]interface{}{
			"mode": "casual_3v3",
		})
		if err != nil {
			logger.Error("MatchCreate from matchmaker failed: %v", err)
			return "", err
		}
		return matchID, nil
	}); err != nil {
		return err
	}

	logger.Info("Ashfold registered match=%s rpc=%s,%s matchmaker", matchName, rpcHealth, rpcCreateDebugMatch)
	return nil
}

func rpcHealthHandler(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	out, _ := json.Marshal(map[string]interface{}{
		"ok":      true,
		"service": "ashfold",
		"time":    time.Now().UTC().Format(time.RFC3339),
		"match":   matchName,
	})
	return string(out), nil
}

func rpcCreateDebugMatchHandler(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	params := map[string]interface{}{
		"debug": true,
	}
	matchID, err := nk.MatchCreate(ctx, matchName, params)
	if err != nil {
		logger.Error("MatchCreate failed: %v", err)
		return "", err
	}
	out, _ := json.Marshal(map[string]string{"match_id": matchID})
	return string(out), nil
}
