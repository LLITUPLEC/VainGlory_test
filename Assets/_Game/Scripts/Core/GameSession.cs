using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ashfold
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession I { get; private set; }

        public IAuthService Auth { get; private set; }
        public NakamaConnection Nakama { get; private set; }
        public NakamaMatchClient MatchClient { get; private set; }
        public NakamaSocial Social { get; private set; }
        public PlayerProfile Profile { get; private set; }
        public bool IsAuthenticated => Profile != null;
        public string ShowcaseHeroId = "bastion";
        public MatchSession Match;
        public MatchResult LastResult;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (I != null)
                return;
            var go = new GameObject("GameSession");
            DontDestroyOnLoad(go);
            go.AddComponent<GameSession>();
        }

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }

            I = this;
            Nakama = new NakamaConnection();
            MatchClient = gameObject.GetComponent<NakamaMatchClient>() ?? gameObject.AddComponent<NakamaMatchClient>();
            Social = gameObject.GetComponent<NakamaSocial>() ?? gameObject.AddComponent<NakamaSocial>();
            if (NakamaConfig.UseServer)
            {
                Auth = new NakamaAuthService(Nakama);
                Debug.Log("[Ashfold] Auth = NakamaAuthService");
            }
            else
            {
                Auth = new DevAuthService();
                Debug.Log("[Ashfold] Auth = DevAuthService (set NakamaConfig.UseServer=true for VPS)");
            }
        }

        void Start()
        {
            StartCoroutine(WatchSessionClaim());
        }

        IEnumerator WatchSessionClaim()
        {
            yield return new WaitForSeconds(3f);
            var delay = 6f;
            while (true)
            {
                if (IsAuthenticated && NakamaConfig.UseServer && Nakama != null && Auth != null && Nakama.Session != null)
                {
                    var ready = Nakama.EnsureSessionAsync();
                    while (!ready.IsCompleted)
                        yield return null;
                    if (ready.Status == TaskStatus.RanToCompletion && ready.Result)
                    {
                        delay = 6f;
                        var device = Auth.DeviceId;
                        var task = NakamaSessionClaim.TakenOverAsync(Nakama, device);
                        while (!task.IsCompleted)
                            yield return null;
                        if (task.Status == TaskStatus.RanToCompletion && task.Result)
                        {
                            NakamaSessionClaim.MarkKicked();
                            SignOut();
                            SceneManager.LoadScene(AppScenes.Boot);
                        }
                    }
                    else
                        delay = Mathf.Min(delay * 2f, 60f);
                }
                yield return new WaitForSeconds(delay);
            }
        }

        public void SetProfile(PlayerProfile profile)
        {
            Profile = profile;
        }

        public Task SaveProgressAsync()
        {
            if (Auth == null || Profile == null)
                return Task.CompletedTask;
            return Auth.SaveProgressAsync(Profile);
        }

        public void SignOut()
        {
            if (Social != null)
                Social.ResetLocal();
            if (Nakama != null)
            {
                var _ = Nakama.DisconnectRealtimeAsync();
            }
            Auth?.SignOutLocal();
            Profile = null;
            Match = null;
            LastResult = null;
        }
    }
}
