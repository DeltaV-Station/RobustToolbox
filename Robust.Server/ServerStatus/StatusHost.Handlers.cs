using System;
using System.Net;
using Newtonsoft.Json.Linq;
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
            AddHandler(HandleAutomaticClientZip);
        }

        private static bool HandleTeapot(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/teapot")
            {
                return false;
            }

            context.Respond("I am a teapot.", (HttpStatusCode) 418);
            return true;
        }

        private bool HandleStatus(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/status")
            {
                return false;
            }

            var jObject = new JObject
            {
                // We need to send at LEAST name and player count to have the launcher work with us.
                // Content can override these if it wants (e.g. stealthmins).
                ["name"] = _serverNameCache,
                ["players"] = _playerManager.PlayerCount
            };

            OnStatusRequest?.Invoke(jObject);

            context.RespondJson(jObject);

            return true;
        }

        private bool HandleInfo(IStatusHandlerContext context)
        {
            if (!context.IsGetLike || context.Url!.AbsolutePath != "/info")
            {
                return false;
            }

            var downloadUrl = _configurationManager.GetCVar(CVars.BuildDownloadUrl);

            JObject? buildInfo;

            if (string.IsNullOrEmpty(downloadUrl))
            {
                buildInfo = PrepareACZBuildInfo();
            }
            else
            {
                var hash = _configurationManager.GetCVar(CVars.BuildHash);
                if (hash == "")
                {
                    hash = null;
                }

                buildInfo = new JObject
                {
                    ["engine_version"] = _configurationManager.GetCVar(CVars.BuildEngineVersion),
                    ["fork_id"] = _configurationManager.GetCVar(CVars.BuildForkId),
                    ["version"] = _configurationManager.GetCVar(CVars.BuildVersion),
                    ["download_url"] = downloadUrl,
                    ["hash"] = hash,
                };
            }

            var authInfo = new JObject
            {
                ["mode"] = _netManager.Auth.ToString(),
                ["public_key"] = _netManager.RsaPublicKey != null
                    ? Convert.ToBase64String(_netManager.RsaPublicKey)
                    : null
            };

            var jObject = new JObject
            {
                ["connect_address"] = _configurationManager.GetCVar(CVars.StatusConnectAddress),
                ["auth"] = authInfo,
                ["build"] = buildInfo
            };

            OnInfoRequest?.Invoke(jObject);

            context.RespondJson(jObject);

            return true;
        }

        private JObject? PrepareACZBuildInfo()
        {
            if (PrepareACZ() == null)
            {
                return null;
            }

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
            // Download URL needs to be cheated because of the way status connect address is setup.
            // Personally I'd say get rid of the "udp://" part.
            var selfAddress = _configurationManager.GetCVar(CVars.StatusSelfAddress);
            return new JObject
            {
                ["engine_version"] = engineVersion,
                ["fork_id"] = fork,
                ["version"] = _aczHash,
                ["download_url"] = selfAddress + "acz.zip",
                ["hash"] = _aczHash,
            };
        }
    }

}
