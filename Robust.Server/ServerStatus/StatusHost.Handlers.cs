using System;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json.Nodes;
using Robust.Shared;

namespace Robust.Server.ServerStatus
{

    internal sealed partial class StatusHost
    {

        private void RegisterHandlers()
        {
            AddHandler(HandleTeapot);
            AddHandler(HandleStatus);
            AddHandler(HandleInfo);
            AddAczHandlers();
        }

        private static async Task<bool> HandleTeapot(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/teapot")
            {
                return false;
            }

            await context.RespondAsync("I am a teapot.", (HttpStatusCode) 418);
            return true;
        }

        private async Task<bool> HandleStatus(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/status")
            {
                return false;
            }

            var jObject = new JsonObject
            {
                // We need to send at LEAST name and player count to have the launcher work with us.
                // Content can override these if it wants (e.g. stealthmins).
                ["name"] = _serverNameCache,
                ["players"] = _playerManager.PlayerCount
            };

            OnStatusRequest?.Invoke(jObject);

            await context.RespondJsonAsync(jObject);

            return true;
        }

        private async Task<bool> HandleInfo(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/info")
            {
                return false;
            }

            var downloadUrl = _configurationManager.GetCVar(CVars.BuildDownloadUrl);

            JsonObject? buildInfo;

            if (string.IsNullOrEmpty(downloadUrl))
            {
                buildInfo = await PrepareACZBuildInfo();
            }
            else
            {
                var hash = _configurationManager.GetCVar(CVars.BuildHash);
                if (hash == "")
                {
                    hash = null;
                }

                buildInfo = new JsonObject
                {
                    ["engine_version"] = _configurationManager.GetCVar(CVars.BuildEngineVersion),
                    ["fork_id"] = _configurationManager.GetCVar(CVars.BuildForkId),
                    ["version"] = _configurationManager.GetCVar(CVars.BuildVersion),
                    ["download_url"] = downloadUrl,
                    ["hash"] = hash,
                    ["acz"] = false,
                    ["manifest_download_url"] = _configurationManager.GetCVar(CVars.BuildManifestDownloadUrl),
                    ["manifest_url"] = _configurationManager.GetCVar(CVars.BuildManifestUrl),
                    ["manifest_hash"] = _configurationManager.GetCVar(CVars.BuildManifestHash)
                };
            }

            var authInfo = new JsonObject
            {
                ["mode"] = _netManager.Auth.ToString(),
                ["public_key"] = _netManager.CryptoPublicKey != null
                    ? Convert.ToBase64String(_netManager.CryptoPublicKey)
                    : null
            };

            var jObject = new JsonObject
            {
                ["connect_address"] = _configurationManager.GetCVar(CVars.StatusConnectAddress),
                ["auth"] = authInfo,
                ["build"] = buildInfo
            };

            OnInfoRequest?.Invoke(jObject);

            await context.RespondJsonAsync(jObject);

            return true;
        }

        private async Task<JsonObject?> PrepareACZBuildInfo()
        {
            var acz = await PrepareACZ();
            if (acz == null) return null;

            // Automatic - pass to ACZ
            // Unfortunately, we still can't divine engine version.
            var engineVersion = _configurationManager.GetCVar(CVars.BuildEngineVersion);
            // Fork ID is an interesting case, we don't want to cause too many redownloads but we also don't want to pollute disk.
            // Call the fork "custom" if there's no explicit ID given.
            var fork = _configurationManager.GetCVar(CVars.BuildForkId);
            if (string.IsNullOrEmpty(fork))
            {
                fork = "custom";
            }
            return new JsonObject
            {
                ["engine_version"] = engineVersion,
                ["fork_id"] = fork,
                ["version"] = acz.ManifestHash,
                // Don't supply a download URL - like supplying an empty self-address
                ["download_url"] = "",
                ["manifest_download_url"] = "",
                ["manifest_url"] = "",
                // Pass acz so the launcher knows where to find the downloads.
                ["acz"] = true,
                ["hash"] = acz.ZipHash,
                ["manifest_hash"] = acz.ManifestHash
            };
        }
    }

}
