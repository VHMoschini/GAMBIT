using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Prototipo.Editor
{
    /// <summary>
    /// Garante automaticamente que toda build Desktop (Windows/Linux/macOS) receba o arquivo
    /// <c>steam_appid.txt</c> com o AppID de testes da Steam (<c>480</c> - Spacewar) ao lado do executavel,
    /// permitindo testar multiplayer pela Steam como jogo nao-Steam sem passos manuais.
    /// </summary>
    public sealed class PrototypeSteamBuildProcessor : IPostprocessBuildWithReport
    {
        public const string TestSteamAppId = "480";

        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows64
                && target != BuildTarget.StandaloneWindows
                && target != BuildTarget.StandaloneLinux64
                && target != BuildTarget.StandaloneOSX)
            {
                return;
            }

            string outputPath = report.summary.outputPath;
            string outputDir = Directory.Exists(outputPath)
                ? outputPath
                : Path.GetDirectoryName(outputPath);

            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                return;
            }

            WriteAppIdFile(outputDir);
        }

        public static string WriteAppIdFile(string folderPath)
        {
            string appIdPath = Path.Combine(folderPath, "steam_appid.txt");
            File.WriteAllText(appIdPath, TestSteamAppId);
            Debug.Log("[PrototypeSteamBuildProcessor] 'steam_appid.txt' (" + TestSteamAppId + ") gravado em: " + appIdPath);
            return appIdPath;
        }
    }
}
