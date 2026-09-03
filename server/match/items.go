package match

import (
	"encoding/json"
	"math"
)

const (
	maxItems   = 6
	heroismMul = 4.0
)

type itemDef struct {
	ID          string
	Cost        int
	BonusDamage float64
	BonusAS     float64
	BonusHP     float64
	BonusResist float64
	BonusHeal   float64
	BonusMove   float64
	BuffOnly    bool
}

var itemDefs = map[string]itemDef{
	"iron_edge":    {ID: "iron_edge", Cost: 150, BonusDamage: 25},
	"storm_charm":  {ID: "storm_charm", Cost: 150, BonusAS: 0.25},
	"stoneplate":   {ID: "stoneplate", Cost: 150, BonusHP: 180},
	"wardcloak":    {ID: "wardcloak", Cost: 150, BonusResist: 0.18},
	"lifewell":     {ID: "lifewell", Cost: 150, BonusHeal: 0.4},
	"pulse_beacon": {ID: "pulse_beacon", Cost: 150, BonusMove: 0.12},
	"heroism":      {ID: "heroism", Cost: 1500, BuffOnly: true},
}

func preferredItem(heroId string) string {
	plan := shopPlan(heroId)
	if len(plan) == 0 {
		return "iron_edge"
	}
	return plan[0]
}

func shopPlan(heroId string) []string {
	switch heroId {
	case "bastion":
		// танк: 2 HP, 3 resist, 1 dmg
		return []string{"stoneplate", "stoneplate", "wardcloak", "wardcloak", "wardcloak", "iron_edge"}
	case "mira":
		// хил: 2 heal, 1 move, 2 HP, 1 resist
		return []string{"lifewell", "lifewell", "pulse_beacon", "stoneplate", "stoneplate", "wardcloak"}
	default:
		// дамагер: 3 dmg, 2 HP, 1 resist
		return []string{"iron_edge", "storm_charm", "iron_edge", "stoneplate", "stoneplate", "wardcloak"}
	}
}

func tryBuyNext(h *hero) bool {
	if h == nil {
		return false
	}
	plan := shopPlan(h.HeroId)
	if len(h.Items) >= maxItems || len(h.Items) >= len(plan) {
		return false
	}
	return tryBuy(h, plan[len(h.Items)])
}

func applyBuyInput(s *State, userId string, data []byte) {
	if s.Phase != phaseCombat {
		return
	}
	h := heroByUser(s, userId)
	if h == nil || !h.Alive || h.Bot {
		return
	}
	var dto struct {
		ItemId string `json:"itemId"`
		Seq    int    `json:"seq"`
	}
	if json.Unmarshal(data, &dto) != nil {
		return
	}
	if staleSeq(h, dto.Seq) {
		return
	}
	tryBuy(h, dto.ItemId)
}

func tryBuy(h *hero, itemId string) bool {
	if h == nil || !inFountain(h) {
		return false
	}
	it, ok := itemDefs[itemId]
	if !ok || h.Gold < it.Cost {
		return false
	}
	if it.BuffOnly {
		if h.Heroism {
			return false
		}
		h.Gold -= it.Cost
		h.Heroism = true
		applyHeroItems(h)
		return true
	}
	if len(h.Items) >= maxItems {
		return false
	}
	h.Gold -= it.Cost
	h.Items = append(h.Items, it.ID)
	applyHeroItems(h)
	return true
}

func applyHeroItems(h *hero) {
	if h == nil {
		return
	}
	def := resolveHero(h.HeroId)
	var dmg, as, hp, resist, heal, move float64
	for _, id := range h.Items {
		it, ok := itemDefs[id]
		if !ok || it.BuffOnly {
			continue
		}
		dmg += it.BonusDamage
		as += it.BonusAS
		hp += it.BonusHP
		resist += it.BonusResist
		heal += it.BonusHeal
		move += it.BonusMove
	}
	mul := 1.0
	if h.Heroism {
		mul = heroismMul
	}
	newMax := (def.MaxHP + hp) * mul
	if newMax > h.MaxHP {
		h.HP += newMax - h.MaxHP
	}
	h.MaxHP = newMax
	if h.HP > h.MaxHP {
		h.HP = h.MaxHP
	}
	h.Damage = (def.Damage + dmg) * mul
	h.Interval = def.Interval / math.Max(0.4, 1+as)
	h.Speed = def.MoveSpeed * (1 + move)
	h.Resist = clamp(resist*mul, 0, 0.9)
	h.HealPower = heal * mul
}

func scaledSkill(h *hero, def heroDef) float64 {
	if h == nil {
		return def.SkillPower
	}
	return def.SkillPower * (1 + h.HealPower)
}

func itemsCsv(h *hero) string {
	if h == nil || len(h.Items) == 0 {
		return ""
	}
	out := h.Items[0]
	for i := 1; i < len(h.Items); i++ {
		out += "," + h.Items[i]
	}
	return out
}
