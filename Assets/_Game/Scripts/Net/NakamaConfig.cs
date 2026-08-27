namespace Ashfold
{
    /// <summary>
        /// Внешний вход: https://api.prokrust-play.ru (Caddy → 127.0.0.1:7350).
    /// </summary>
    public static class NakamaConfig
    {
        public static readonly bool UseServer = true;

        public const string Scheme = "https";
        public const string Host = "api.prokrust-play.ru";
        public const int Port = 443;

        /// <summary>Совпадает с --socket.server_key в server/docker-compose.yml</summary>
        public const string ServerKey = "gDNVymCHsgbFr6QL4ENkImtds7Bu3T7bi1TG9QUDE0U=";

        public const int TimeoutSeconds = 15;
    }
}
