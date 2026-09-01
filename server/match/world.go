package match

import "math"

const (
	idTurretDawn  = 10
	idTurretDusk  = 11
	idCrystalDawn = 12
	idCrystalDusk = 13
	kindMinion    = "minion"
	kindCaptain   = "captain"
	kindTurret    = "turret"
	kindCrystal   = "crystal"
	kindCamp      = "camp"
	teamNeutral   = 2
	idCampNL      = 20
	idCampNR      = 21
	idCampSL      = 22
	idCampSR      = 23
	campHP        = 220.0
	campDmg       = 22.0
	campRange     = 1.8
	campAggro     = 3.5
	campSpeed     = 3.2
	campInterval  = 1.1
	campBounty    = 28
	campRespawn   = 18.0
	campLeash     = 8.0
	minionHP      = 170.0
	minionDmg     = 16.0
	minionRange   = 1.7
	minionSpeed   = 3.68
	minionInterval = 1.1
	minionBounty  = 14
	captainHP     = 250.0
	captainDmg    = 22.0
	captainBounty = 24
	captainScale  = 1.5
	waveSize      = 4
	waveSpacing   = 1.45
	turretHP      = 1540.0
	turretDmg     = 119.0
	turretRange   = 9.0
	turretInterval = 1.15
	crystalHP     = 2800.0
	turretSolidR  = 2.2
	crystalSolidR = 2.8
	moverSolidR   = 0.4
	waveIntervalTicks = 20 * tickRate
	maxLiveMinions    = 32
)

type ent struct {
	ID           int
	Kind         string
	Team         int
	X, Z, Yaw    float64
	HP, MaxHP    float64
	Damage       float64
	Range        float64
	Interval     float64
	Speed        float64
	AttackCd     float64
	AttackTarget int
	Alive        bool
	Bounty       int
	GroundY      float64
	HomeX, HomeZ float64
	RespawnLeft  float64
}

func spawnWorld(s *State) {
	if s.Extras == nil {
		s.Extras = make(map[int]*ent)
	}
	s.Extras[idTurretDawn] = structure(idTurretDawn, kindTurret, 0, -16, turretHP, turretDmg, turretRange, turretInterval, 0)
	s.Extras[idTurretDusk] = structure(idTurretDusk, kindTurret, 1, 16, turretHP, turretDmg, turretRange, turretInterval, 120)
	s.Extras[idCrystalDawn] = structure(idCrystalDawn, kindCrystal, 0, -halfLength-3.5, crystalHP, 0, 0, 1, 0)
	s.Extras[idCrystalDusk] = structure(idCrystalDusk, kindCrystal, 1, halfLength+3.5, crystalHP, 0, 0, 1, 200)
	spawnCamp(s, idCampNL, -12, 13)
	spawnCamp(s, idCampNR, 12, 13)
	spawnCamp(s, idCampSL, -12, -13)
	spawnCamp(s, idCampSR, 12, -13)
	if s.NextMinionId < 100 {
		s.NextMinionId = 100
	}
}

func structure(id int, kind string, team int, x, hp, dmg, rng, interval float64, bounty int) *ent {
	yaw := 90.0
	if team == 1 {
		yaw = -90
	}
	return &ent{
		ID: id, Kind: kind, Team: team,
		X: x, Z: 0, Yaw: yaw,
		HP: hp, MaxHP: hp,
		Damage: dmg, Range: rng, Interval: interval,
		Alive: true, Bounty: bounty, GroundY: 1.6,
	}
}

func spawnCamp(s *State, id int, x, z float64) {
	s.Extras[id] = &ent{
		ID: id, Kind: kindCamp, Team: teamNeutral,
		X: x, Z: z, HomeX: x, HomeZ: z,
		HP: campHP, MaxHP: campHP,
		Damage: campDmg, Range: campRange, Interval: campInterval,
		Speed: campSpeed, Alive: true, Bounty: campBounty, GroundY: 0.4,
	}
}

func tickWorld(s *State) {
	s.WaveTicks--
	if s.WaveTicks <= 0 {
		s.WaveTicks = waveIntervalTicks
		spawnWave(s)
	}
	for _, u := range s.Extras {
		stepExtra(s, u)
	}
	pruneMinions(s)
}

func spawnWave(s *State) {
	live := 0
	for _, u := range s.Extras {
		if u != nil && isLaneCreep(u) && u.Alive {
			live++
		}
	}
	if live >= maxLiveMinions {
		return
	}
	for team := 0; team < 2; team++ {
		x := -32.0
		dir := 1.0
		if team == 1 {
			x = 32
			dir = -1
		}
		for i := 0; i < waveSize; i++ {
			id := s.NextMinionId
			s.NextMinionId++
			along := float64(waveSize-1-i) * waveSpacing
			px := x + dir*along
			captain := i == waveSize-1
			kind := kindMinion
			hp := minionHP
			dmg := minionDmg
			bounty := minionBounty
			gy := 0.7
			rng := minionRange
			if captain {
				kind = kindCaptain
				hp = captainHP
				dmg = captainDmg
				bounty = captainBounty
				gy = 0.7 * captainScale
				rng = 1.9
			}
			s.Extras[id] = &ent{
				ID: id, Kind: kind, Team: team,
				X: px, Z: 0, Yaw: yawToward(px, 0, -x, 0),
				HP: hp, MaxHP: hp,
				Damage: dmg, Range: rng, Interval: minionInterval,
				Speed: minionSpeed, Alive: true, Bounty: bounty, GroundY: gy,
			}
		}
	}
}

func stepExtra(s *State, u *ent) {
	if u == nil {
		return
	}
	if u.Kind == kindCamp {
		if u.Alive && u.AttackCd > 0 {
			u.AttackCd -= dt
		}
		stepCamp(s, u)
		return
	}
	if !u.Alive {
		return
	}
	if u.AttackCd > 0 {
		u.AttackCd -= dt
	}
	switch u.Kind {
	case kindTurret:
		stepTurret(s, u)
	case kindMinion, kindCaptain:
		stepMinion(s, u)
	}
}

func stepTurret(s *State, u *ent) {
	tid := nearestHostile(s, u.X, u.Z, u.Team, u.Range, true)
	if tid == 0 {
		return
	}
	u.AttackTarget = tid
	x, z, _, ok := liveXZ(s, tid)
	if !ok {
		return
	}
	u.Yaw = yawToward(u.X, u.Z, x, z)
	if u.AttackCd > 0 {
		return
	}
	u.AttackCd = u.Interval
	hurt(s, u.ID, u.Team, false, u.Damage, tid)
}

func stepMinion(s *State, u *ent) {
	tid := nearestHostile(s, u.X, u.Z, u.Team, 5.2, false)
	if tid != 0 {
		u.AttackTarget = tid
		x, z, _, ok := liveXZ(s, tid)
		if !ok {
			return
		}
		d := dist(u.X, u.Z, x, z)
		u.Yaw = yawToward(u.X, u.Z, x, z)
		if d > u.Range {
			moveEnt(s, u, x, z)
			return
		}
		if u.AttackCd > 0 {
			return
		}
		u.AttackCd = u.Interval
		hurt(s, u.ID, u.Team, false, u.Damage, tid)
		return
	}
	goal := fountainX(1 - u.Team)
	moveEnt(s, u, goal, 0)
}

func stepCamp(s *State, u *ent) {
	if !u.Alive {
		u.RespawnLeft -= dt
		if u.RespawnLeft <= 0 {
			u.Alive = true
			u.HP = u.MaxHP
			u.X = u.HomeX
			u.Z = u.HomeZ
			u.AttackTarget = 0
		}
		return
	}
	if dist(u.X, u.Z, u.HomeX, u.HomeZ) > campLeash {
		u.AttackTarget = 0
		moveEnt(s, u, u.HomeX, u.HomeZ)
		return
	}
	tid := nearestHostile(s, u.X, u.Z, u.Team, campAggro, false)
	if tid != 0 {
		u.AttackTarget = tid
		x, z, _, ok := liveXZ(s, tid)
		if !ok {
			return
		}
		d := dist(u.X, u.Z, x, z)
		u.Yaw = yawToward(u.X, u.Z, x, z)
		if d > u.Range {
			moveEnt(s, u, x, z)
			return
		}
		if u.AttackCd > 0 {
			return
		}
		u.AttackCd = u.Interval
		hurt(s, u.ID, u.Team, false, u.Damage, tid)
		return
	}
	if dist(u.X, u.Z, u.HomeX, u.HomeZ) > arriveEps {
		moveEnt(s, u, u.HomeX, u.HomeZ)
	}
}

func moveEnt(s *State, u *ent, x, z float64) {
	dx := x - u.X
	dz := z - u.Z
	d := math.Hypot(dx, dz)
	if d <= arriveEps {
		u.X, u.Z = clampSolid(s, x, z)
		return
	}
	step := u.Speed * dt
	if step >= d {
		u.X, u.Z = clampSolid(s, x, z)
	} else {
		u.X, u.Z = clampSolid(s, u.X+dx/d*step, u.Z+dz/d*step)
	}
	u.Yaw = yawToward(u.X-dx, u.Z-dz, x, z)
}

func clampSolid(s *State, x, z float64) (float64, float64) {
	x, z = resolveSolid(s, x, z)
	return clamp(x, -halfLength-mapPad, halfLength+mapPad), clamp(z, -halfWidth, halfWidth)
}

func resolveSolid(s *State, x, z float64) (float64, float64) {
	if s == nil {
		return x, z
	}
	for _, u := range s.Extras {
		if u == nil || !u.Alive {
			continue
		}
		var body float64
		switch u.Kind {
		case kindTurret:
			body = turretSolidR
		case kindCrystal:
			body = crystalSolidR
		default:
			continue
		}
		need := body + moverSolidR
		dx := x - u.X
		dz := z - u.Z
		d := math.Hypot(dx, dz)
		if d >= need {
			continue
		}
		if d < 0.001 {
			dx, dz, d = 1, 0, 1
		}
		x = u.X + dx/d*need
		z = u.Z + dz/d*need
	}
	return x, z
}

func nearestHostile(s *State, x, z float64, team int, rng float64, preferHero bool) int {
	bestHero, bestOther := 0, 0
	bestHeroD, bestOtherD := rng, rng
	for _, h := range s.Heroes {
		if h == nil || !h.Alive || h.Team == team {
			continue
		}
		d := dist(x, z, h.X, h.Z)
		if d <= bestHeroD {
			bestHeroD = d
			bestHero = h.ID
		}
	}
	for _, u := range s.Extras {
		if u == nil || !u.Alive || u.Team == team {
			continue
		}
		d := dist(x, z, u.X, u.Z)
		if d > rng {
			continue
		}
		if d < bestOtherD {
			bestOtherD = d
			bestOther = u.ID
		}
	}
	if preferHero && bestHero != 0 {
		return bestHero
	}
	if bestHero != 0 && bestHeroD <= rng {
		if !preferHero && bestOther != 0 && bestOtherD < bestHeroD {
			return bestOther
		}
		return bestHero
	}
	return bestOther
}

func liveXZ(s *State, id int) (float64, float64, int, bool) {
	if h := s.Heroes[id]; h != nil && h.Alive {
		return h.X, h.Z, h.Team, true
	}
	if u := s.Extras[id]; u != nil && u.Alive {
		return u.X, u.Z, u.Team, true
	}
	return 0, 0, 0, false
}

func hurt(s *State, srcID, srcTeam int, srcHero bool, dmg float64, dstID int) {
	hurtHit(s, srcID, srcTeam, srcHero, dmg, dstID, 0)
}

func hurtSkill(s *State, srcID, srcTeam int, srcHero bool, dmg float64, dstID int) {
	hurtHit(s, srcID, srcTeam, srcHero, dmg, dstID, 1)
}

func hurtHit(s *State, srcID, srcTeam int, srcHero bool, dmg float64, dstID int, skill int) {
	if dmg <= 0 {
		return
	}
	kill := 0
	if h := s.Heroes[dstID]; h != nil && h.Alive {
		cancelRecall(h)
		taken := dmg * (1 - clamp(h.Resist, 0, 0.9))
		h.HP -= taken
		if h.HP <= 0 {
			h.HP = 0
			h.Alive = false
			h.Deaths++
			h.AttackTarget = 0
			h.HasMove = false
			h.RespawnLeft = respawnSeconds(s)
			kill = 1
			if srcHero {
				if src := s.Heroes[srcID]; src != nil {
					src.Kills++
					src.Gold += heroBounty
				}
			}
		}
		s.Hits = append(s.Hits, hitEvent{Src: srcID, Dst: dstID, Dmg: taken, Kill: kill, Skill: skill})
		return
	}
	u := s.Extras[dstID]
	if u == nil || !u.Alive {
		return
	}
	u.HP -= dmg
	if u.HP <= 0 {
		u.HP = 0
		u.Alive = false
		u.AttackTarget = 0
		kill = 1
		if srcHero {
			if src := s.Heroes[srcID]; src != nil {
				src.Gold += u.Bounty
			}
		}
		if u.Kind == kindCrystal {
			endMatch(s, 1-u.Team, false)
		}
		if u.Kind == kindCamp {
			u.RespawnLeft = campRespawn
		}
	}
	s.Hits = append(s.Hits, hitEvent{Src: srcID, Dst: dstID, Dmg: dmg, Kill: kill, Skill: skill})
}

func pruneMinions(s *State) {
	for id, u := range s.Extras {
		if u != nil && isLaneCreep(u) && !u.Alive {
			delete(s.Extras, id)
		}
	}
}

func extraSnapshot(s *State, ents []snapEntity) []snapEntity {
	ids := []int{idTurretDawn, idTurretDusk, idCrystalDawn, idCrystalDusk, idCampNL, idCampNR, idCampSL, idCampSR}
	for _, id := range ids {
		u := s.Extras[id]
		if u == nil {
			continue
		}
		ents = append(ents, snapFromEnt(u))
	}
	for id, u := range s.Extras {
		if u == nil || !isLaneCreep(u) || !u.Alive {
			continue
		}
		if id < 100 {
			continue
		}
		ents = append(ents, snapFromEnt(u))
	}
	return ents
}

func snapFromEnt(u *ent) snapEntity {
	return snapEntity{
		ID: u.ID, Kind: u.Kind, Team: u.Team,
		X: u.X, Z: u.Z, Yaw: u.Yaw,
		HP: u.HP, MaxHP: u.MaxHP,
		Alive: u.Alive, TargetId: u.AttackTarget,
	}
}

func isLaneCreep(u *ent) bool {
	return u != nil && (u.Kind == kindMinion || u.Kind == kindCaptain)
}
