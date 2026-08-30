using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace Ashfold
{
    /// <summary>Nakama Storage: уровень, Essence, открытые герои (этап 1.7 / 4.5).</summary>
    public static class NakamaProgress
    {
        public const string Collection = "progress";
        public const string Key = "meta";
        const string PrefsEssence = "ashfold.progress.essence";
        const string PrefsLevel = "ashfold.progress.level";
        const string PrefsHeroes = "ashfold.progress.heroes";
        const string PrefsShowcase = "ashfold.progress.showcase";

        [Serializable]
        public sealed class Blob
        {
            public int level = 1;
            public int essence;
            public string showcaseHeroId = "bastion";
            public string unlockedHeroes = "bastion,vesper,mira";
        }

        public static async Task HydrateAsync(NakamaConnection conn, PlayerProfile profile)
        {
            if (conn == null || conn.Session == null || profile == null)
            {
                ApplyLocal(profile);
                return;
            }

            try
            {
                if (!await conn.EnsureSessionAsync())
                {
                    ApplyLocal(profile);
                    return;
                }
                var result = await conn.Client.ReadStorageObjectsAsync(conn.Session, new IApiReadStorageObjectId[]
                {
                    new StorageObjectId
                    {
                        Collection = Collection,
                        Key = Key,
                        UserId = conn.Session.UserId
                    }
                });

                Blob blob = null;
                if (result?.Objects != null)
                {
                    foreach (var obj in result.Objects)
                    {
                        if (!string.IsNullOrEmpty(obj.Value))
                            blob = JsonUtility.FromJson<Blob>(obj.Value);
                        break;
                    }
                }

                if (blob == null)
                {
                    blob = FromProfile(profile);
                    await WriteAsync(conn, blob);
                }

                Apply(profile, blob);
                CacheLocal(blob);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Progress read failed, using local: " + e.Message);
                ApplyLocal(profile);
            }
        }

        public static async Task PushAsync(NakamaConnection conn, PlayerProfile profile)
        {
            var blob = FromProfile(profile);
            CacheLocal(blob);
            if (conn == null || conn.Session == null)
                return;

            try
            {
                if (!await conn.EnsureSessionAsync())
                    return;
                await WriteAsync(conn, blob);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ashfold] Progress write failed: " + e.Message);
            }
        }

        static async Task WriteAsync(NakamaConnection conn, Blob blob)
        {
            var json = JsonUtility.ToJson(blob);
            await conn.Client.WriteStorageObjectsAsync(conn.Session, new IApiWriteStorageObject[]
            {
                new WriteStorageObject
                {
                    Collection = Collection,
                    Key = Key,
                    Value = json,
                    PermissionRead = 1,
                    PermissionWrite = 1
                }
            });
        }

        static Blob FromProfile(PlayerProfile profile)
        {
            var showcase = GameSession.I != null ? GameSession.I.ShowcaseHeroId : "bastion";
            return new Blob
            {
                level = profile != null && profile.Level > 0 ? profile.Level : 1,
                essence = profile != null ? profile.Essence : 0,
                showcaseHeroId = string.IsNullOrEmpty(showcase) ? "bastion" : showcase,
                unlockedHeroes = profile != null && !string.IsNullOrEmpty(profile.UnlockedHeroesCsv)
                    ? profile.UnlockedHeroesCsv
                    : "bastion,vesper,mira"
            };
        }

        static void Apply(PlayerProfile profile, Blob blob)
        {
            if (profile == null || blob == null)
                return;
            profile.Level = blob.level < 1 ? 1 : blob.level;
            profile.Essence = blob.essence;
            profile.UnlockedHeroesCsv = string.IsNullOrEmpty(blob.unlockedHeroes)
                ? "bastion,vesper,mira"
                : blob.unlockedHeroes;
            if (GameSession.I != null && !string.IsNullOrEmpty(blob.showcaseHeroId))
                GameSession.I.ShowcaseHeroId = blob.showcaseHeroId;
        }

        static void ApplyLocal(PlayerProfile profile)
        {
            if (profile == null)
                return;
            profile.Level = PlayerPrefs.GetInt(PrefsLevel, profile.Level);
            profile.Essence = PlayerPrefs.GetInt(PrefsEssence, profile.Essence);
            profile.UnlockedHeroesCsv = PlayerPrefs.GetString(PrefsHeroes, profile.UnlockedHeroesCsv);
            if (GameSession.I != null)
                GameSession.I.ShowcaseHeroId = PlayerPrefs.GetString(PrefsShowcase, GameSession.I.ShowcaseHeroId);
        }

        static void CacheLocal(Blob blob)
        {
            PlayerPrefs.SetInt(PrefsLevel, blob.level);
            PlayerPrefs.SetInt(PrefsEssence, blob.essence);
            PlayerPrefs.SetString(PrefsHeroes, blob.unlockedHeroes);
            PlayerPrefs.SetString(PrefsShowcase, blob.showcaseHeroId);
            PlayerPrefs.Save();
        }
    }
}
