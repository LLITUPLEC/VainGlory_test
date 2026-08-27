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
            while (true)
            {
                if (IsAuthenticated && NakamaConfig.UseServer && Nakama != null && Nakama.IsConnected && Auth != null)
                {
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
                yield return new WaitForSeconds(6f);
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
