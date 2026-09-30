using System.IO;
using UnityEditor;

namespace VRTraining.Editor
{
    // Пост-процессор проектов решений.
    // Вычищает старый GUID инструментов Unity из .csproj, чтобы Visual Studio 2022
    // открывала проекты напрямую без ошибок совместимости.
    [InitializeOnLoad]
    public class CleanCSProjectPostprocessor : AssetPostprocessor
    {
        static CleanCSProjectPostprocessor()
        {
            EditorApplication.delayCall += EnsureVSEditorSelected;
        }

        private static void EnsureVSEditorSelected()
        {
            string vsPath = @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe";
            if (File.Exists(vsPath))
            {
                EditorPrefs.SetString("kScriptsDefaultApp", vsPath);
            }
        }

        private static string OnGeneratedCSProject(string path, string content)
        {
            // Убираем устаревший project flavor GUID, вызывающий статус "(несовместимо)" в современных версиях VS
            if (content.Contains("{E097FAD1-6243-4DAD-9C02-E9B9EFC3FFC1};"))
            {
                content = content.Replace("{E097FAD1-6243-4DAD-9C02-E9B9EFC3FFC1};", "");
            }
            return content;
        }
    }
}
