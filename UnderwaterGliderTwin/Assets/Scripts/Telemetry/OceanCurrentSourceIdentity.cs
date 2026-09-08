using System;
using System.Security.Cryptography;
using System.Text;

namespace UnderwaterGliderTwin.Telemetry
{
    [Serializable]
    public sealed class OceanCurrentSourceIdentity
    {
        public string Kind;
        public string NormalizedPath;
        public string ContentSha256;
        public int SchemaVersion;
        public string CacheToken;

        public static OceanCurrentSourceIdentity ForLocalFile(string normalizedPath, string contentSha256, int schemaVersion)
        {
            var identity = new OceanCurrentSourceIdentity
            {
                Kind = "local-file",
                NormalizedPath = normalizedPath ?? string.Empty,
                ContentSha256 = contentSha256 ?? string.Empty,
                SchemaVersion = schemaVersion
            };
            identity.CacheToken = Sha256(identity.Kind + "|" + identity.NormalizedPath + "|" + identity.ContentSha256 + "|" + identity.SchemaVersion);
            return identity;
        }

        public static string Sha256(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var valueByte in bytes) builder.Append(valueByte.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
