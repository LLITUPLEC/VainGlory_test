package match

import "math"

const (
	idCrystalDawn = 12
	idCrystalDusk = 13
	kindMinion    = "minion"
	kindCaptain   = "captain"
	kindTurret    = "turret"
	kindCrystal   = "crystal"
	kindCamp      = "camp"
	kindBoss      = "boss"
	teamNeutral   = 2
	idCampL1      = 20
	idCampL2      = 21
	idCampR1      = 22
	idCampR2      = 23
	idBoss        = 24
	idTurretL1    = 30
	idTurretL2    = 31
	idTurretL3    = 32
	idTurretL4    = 33
	idTurretL5    = 34
	idTurretR1    = 35
	idTurretR2    = 36
	idTurretR3    = 37
	idTurretR4    = 38
	idTurretR5    = 39
	campHP        = 220.0
	campDmg       = 22.0
	campRange     = 1.8
	campAggro     = 3.5
	campSpeed     = 3.2
	campInterval  = 1.1
	campBounty    = 28
	campRespawn   = 18.0
	campLeash     = 8.0
	bossHP        = 4800.0
	bossDmg       = 72.0
	bossRange     = 2.8
	bossAggro     = 8.0
	bossSpeed     = 2.4
	bossInterval  = 1.35
	bossBounty    = 300
	bossSpawnSec  = 420.0
	bossX         = 12.792
	bossZ         = -5.112
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
	turretLockHp  = 0.2
	turretUnlockR = 9.0
	laneShoulder  = 3.8
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
	LaneI        int
}

func spawnWorld(s *State) {
	if s.Extras == nil {
		s.Extras = make(map[int]*ent)
	}
	s.Extras[idTurretL1] = structureAt(idTurretL1, kindTurret, 0, -83.460, -5.904, turretHP, turretDmg, turretRange, turretInterval, 0)
	s.Extras[idTurretL2] = structureAt(idTurretL2, kindTurret, 0, -68.700, -15.624, turretHP, turretDmg, turretRange, turretInterval, 120)
	s.Extras[idTurretL3] = structureAt(idTurretL3, kindTurret, 0, -60.408, 22.128, turretHP, turretDmg, turretRange, turretInterval, 120)
	s.Extras[idTurretL4] = structureAt(idTurretL4, kindTurret, 0, -33.000, 24.720, turretHP, turretDmg, turretRange, turretInterval, 120)
	s.Extras[idTurretL5] = structureAt(idTurretL5, kindTurret, 0, -1.800, 24.960, turretHP, turretDmg, turretRange, turretInterval, 120)
	s.Extras[idTurretR1] = structureAt(idTurretR1, kindTurret, 1, 93.960, -11.280, turretHP, turretDmg, turretRange, turretInterval, 0)
	s.Extras[idTurretR2] = structureAt(idTurretR2, kindTurret, 1, 111.840, -7.920, turretHP, turretDmg, turretRange, turretInterval, 120)
	s.Extras[idTurretR3] = structureAt(idTurretR3, kindTurret, 1, 29.280, 24.960, turretHP, turretDmg, turretRange, turretInterval, 120)
	s.Extras[idTurretR4] = structureAt(idTurretR4, kindTurret, 1, 62.880, 25.680, turretHP, turretDmg, turretRange, turretInterval, 120)
	s.Extras[idTurretR5] = structureAt(idTurretR5, kindTurret, 1, 91.080, 22.200, turretHP, turretDmg, turretRange, turretInterval, 120)
	s.Extras[idCrystalDawn] = structureAt(idCrystalDawn, kindCrystal, 0, crystalDawnX, crystalDawnZ, crystalHP, 0, 0, 1, 0)
	s.Extras[idCrystalDusk] = structureAt(idCrystalDusk, kindCrystal, 1, crystalDuskX, crystalDuskZ, crystalHP, 0, 0, 1, 200)
	spawnCamp(s, idCampL1, -36.612, -11.400)
	spawnCamp(s, idCampL2, -15.336, -0.900)
	spawnCamp(s, idCampR1, 57.168, -13.296)
	spawnCamp(s, idCampR2, 34.752, -2.904)
	if s.NextMinionId < 100 {
		s.NextMinionId = 100
	}
}

func structureAt(id int, kind string, team int, x, z, hp, dmg, rng, interval float64, bounty int) *ent {
	yaw := 90.0
	if team == 1 {
		yaw = -90
	}
	return &ent{
		ID: id, Kind: kind, Team: team,
		X: x, Z: z, Yaw: yaw,
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

func spawnBoss(s *State) {
	s.Extras[idBoss] = &ent{
		ID: idBoss, Kind: kindBoss, Team: teamNeutral,
		X: bossX, Z: bossZ, HomeX: bossX, HomeZ: bossZ,
		HP: bossHP, MaxHP: bossHP,
		Damage: bossDmg, Range: bossRange, Interval: bossInterval,
		Speed: bossSpeed, Alive: true, Bounty: bossBounty, GroundY: 1.2,
	}
}

func tickWorld(s *State) {
	s.WaveTicks--
	if s.WaveTicks <= 0 {
		s.WaveTicks = waveIntervalTicks
		spawnWave(s)
	}
	if s.Extras[idBoss] == nil && float64(s.MatchTimeTicks)*dt >= bossSpawnSec {
		spawnBoss(s)
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
	pts := lanePoints()
	for team := 0; team < 2; team++ {
		cx, cz, dirX, dirZ := laneSpawn(team, pts)
		for i := 0; i < waveSize; i++ {
			id := s.NextMinionId
			s.NextMinionId++
			along := 7.0 + float64(waveSize-1-i)*waveSpacing
			px := cx + dirX*along
			pz := cz + dirZ*along
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
				X: px, Z: pz, Yaw: yawToward(px, pz, px+dirX, pz+dirZ),
				HP: hp, MaxHP: hp,
				Damage: dmg, Range: rng, Interval: minionInterval,
				Speed: minionSpeed, Alive: true, Bounty: bounty, GroundY: gy,
				LaneI: laneStart(team, pts),
			}
		}
	}
}

func stepExtra(s *State, u *ent) {
	if u == nil {
		return
	}
	if u.Kind == kindCamp || u.Kind == kindBoss {
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
		if d > engageDist(s, u.Range, tid) {
			ax, az := approachXZ(u.X, u.Z, x, z, bodyROf(s, tid)+u.Range*0.85)
			moveEnt(s, u, ax, az)
			return
		}
		if u.AttackCd > 0 {
			return
		}
		u.AttackCd = u.Interval
		hurt(s, u.ID, u.Team, false, u.Damage, tid)
		return
	}
	gx, gz := nextLaneFrom(u)
	moveEnt(s, u, gx, gz)
}

func laneSpawn(team int, pts [][2]float64) (cx, cz, dirX, dirZ float64) {
	n := len(pts)
	if n >= 2 {
		if team == 0 {
			cx, cz = pts[0][0], pts[0][1]
			dirX, dirZ = pts[1][0]-cx, pts[1][1]-cz
		} else {
			cx, cz = pts[n-1][0], pts[n-1][1]
			dirX, dirZ = pts[n-2][0]-cx, pts[n-2][1]-cz
		}
		if d := math.Hypot(dirX, dirZ); d > 0.01 {
			return cx, cz, dirX / d, dirZ / d
		}
	}
	cx, cz = crystalXZ(team)
	ox, oz := crystalXZ(1 - team)
	dx, dz := ox-cx, oz-cz
	if d := math.Hypot(dx, dz); d > 0.01 {
		return cx, cz, dx / d, dz / d
	}
	if team == 1 {
		return cx, cz, -1, 0
	}
	return cx, cz, 1, 0
}

func laneStart(team int, pts [][2]float64) int {
	n := len(pts)
	if n == 0 {
		return 0
	}
	if team == 1 {
		return n - 1
	}
	return 0
}

func nextLaneFrom(u *ent) (float64, float64) {
	pts := lanePoints()
	n := len(pts)
	if n == 0 {
		return fountainXZ(1 - u.Team)
	}
	if u.Team == 0 {
		if u.LaneI < 0 {
			u.LaneI = 0
		}
		for u.LaneI < n-1 && reachedLane(u.X, u.Z, pts[u.LaneI], true) {
			u.LaneI++
		}
		if u.LaneI >= n {
			u.LaneI = n - 1
		}
		return pts[u.LaneI][0], pts[u.LaneI][1]
	}
	if u.LaneI < 0 || u.LaneI >= n {
		u.LaneI = n - 1
	}
	for u.LaneI > 0 && reachedLane(u.X, u.Z, pts[u.LaneI], false) {
		u.LaneI--
	}
	return pts[u.LaneI][0], pts[u.LaneI][1]
}

func reachedLane(x, z float64, p [2]float64, dawn bool) bool {
	dx, dz := x-p[0], z-p[1]
	if math.Hypot(dx, dz) <= 2.2 && math.Abs(dx) <= 2.2 {
		return true
	}
	if dawn {
		return x >= p[0]+0.4
	}
	return x <= p[0]-0.4
}

func nextLanePoint(team int, x, z float64) (float64, float64) {
	u := &ent{Team: team, X: x, Z: z, LaneI: -1}
	return nextLaneFrom(u)
}

func lanePoints() [][2]float64 {
	sh := laneShoulder
	return [][2]float64{
		{crystalDawnX, crystalDawnZ},
		{-60.408, 22.128 + sh},
		{-33.000, 24.720 + sh},
		{-1.800, 24.960 + sh},
		{29.280, 24.960 + sh},
		{62.880, 25.680 + sh},
		{91.080, 22.200 + sh},
		{crystalDuskX, crystalDuskZ},
	}
}

func stepCamp(s *State, u *ent) {
	if !u.Alive {
		if u.Kind == kindBoss {
			return
		}
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

func bodyROf(s *State, id int) float64 {
	if s.Heroes[id] != nil {
		return moverSolidR
	}
	if u := s.Extras[id]; u != nil {
		switch u.Kind {
		case kindTurret:
			return turretSolidR
		case kindCrystal:
			return crystalSolidR
		}
	}
	return moverSolidR
}

func engageDist(s *State, atkRange float64, dstID int) float64 {
	return atkRange + moverSolidR + bodyROf(s, dstID)
}

func approachXZ(fromX, fromZ, toX, toZ, stop float64) (float64, float64) {
	dx, dz := toX-fromX, toZ-fromZ
	d := math.Hypot(dx, dz)
	if d < 0.001 || d <= stop {
		return fromX, fromZ
	}
	return toX - dx/d*stop, toZ - dz/d*stop
}

func alliedTurretAlive(s *State, team int) bool {
	for _, u := range s.Extras {
		if u != nil && u.Alive && u.Kind == kindTurret && u.Team == team {
			return true
		}
	}
	return false
}

func enemyMinionNear(s *State, x, z float64, team int, rng float64) bool {
	for _, u := range s.Extras {
		if u == nil || !u.Alive || !isLaneCreep(u) || u.Team == team {
			continue
		}
		if dist(x, z, u.X, u.Z) <= rng {
			return true
		}
	}
	return false
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
		if u.Kind == kindCrystal && alliedTurretAlive(s, u.Team) {
			continue
		}
		if u.Kind == kindTurret && !enemyMinionNear(s, u.X, u.Z, u.Team, turretUnlockR) {
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
					src.GoldEarned += heroBounty
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
	if u.Kind == kindCrystal && alliedTurretAlive(s, u.Team) {
		return
	}
	if u.Kind == kindTurret && !enemyMinionNear(s, u.X, u.Z, u.Team, turretUnlockR) {
		pierce := false
		if srcHero {
			if src := s.Heroes[srcID]; src != nil && src.Heroism {
				pierce = true
			}
		}
		if !pierce {
			floor := u.MaxHP * (1 - turretLockHp)
			if u.HP <= floor+0.01 {
				return
			}
			if u.HP-dmg < floor {
				dmg = u.HP - floor
			}
			if dmg <= 0 {
				return
			}
		}
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
				src.GoldEarned += u.Bounty
				if u.Kind == kindMinion || u.Kind == kindCamp || u.Kind == kindBoss {
					src.CreepKills++
				}
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
	for _, u := range s.Extras {
		if u == nil {
			continue
		}
		if u.Kind == kindTurret || u.Kind == kindCrystal || u.Kind == kindCamp || u.Kind == kindBoss {
			ents = append(ents, snapFromEnt(u))
		}
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
