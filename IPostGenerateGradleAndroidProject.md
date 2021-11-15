# 使用 IPostGenerateGradleAndroidProject 遇到的坑。

如果專案內有使用 IPostGenerateGradleAndroidProject 的話，會在使用 Command line build, batch mode 打包時，IPostGenerateGradleAndroidProject 的內容不會受到 Define 所影響。造成每次使用 Batch mode 都就算 Define symbol 內不含 Define 卻還是會執行 IPostGenerateGradleAndroidProject 的內容。

所以如果有根據不同 Define 使用 IPostGenerateGradleAndroidProject 、然後又不是手動打包的時候，要特別注意。
<br>

除了不受 Define 影響之外，使用 command line, batch mode 包版的時候，只要一呼叫了 executeMethod，似乎就會把 Editor 資料夾內的腳本都讀取進來。

所以就算在 executeMethod 把繼承 IPostGenerateGradleAndroidProject 的腳本刪除，修改，都是無效的。

最後是利用 static bool 來做控制。

附上範例碼:
~~~ c#
class A : IPostGenerateGradleAndroidProject
{
    //加上下行，然後由 excuteMethod 控制
    public static bool IncludeBuild = false; 

    //以下 IPostGenerateGradleAndroidProject 實作。
    public int callbackOrder { get { return 0; } } 
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        //附上 AndroidX 舊版 Unity 作法，但新版應該可以使用自訂 Gradle 來解決。
        string gradlePropertiesFile = parentPath + "/gradle.properties";
        var gradleContentsSb = new StringBuilder();
        if (File.Exists(gradlePropertiesFile))
        {

            string[] oldGradlePropertiesLines = File.ReadAllLines(gradlePropertiesFile);
            foreach (string line in oldGradlePropertiesLines)
            {
                if (!line.Contains("android.useAndroidX=") && !line.Contains("android.enableJetifier="))
                {
                    gradleContentsSb.AppendLine(line);
                }
            }
                
            File.Delete(gradlePropertiesFile);
        }
        StreamWriter writer = File.CreateText(gradlePropertiesFile);
        gradleContentsSb.AppendLine("");
        gradleContentsSb.AppendLine("android.useAndroidX=true");
        gradleContentsSb.AppendLine("android.enableJetifier=true");
        gradleContentsSb.Replace(@"android.enableR8", @"#android.enableR8");

        writer.Write(gradleContentsSb.ToString());
        writer.Flush();
        writer.Close();

        ...
        ...
    }
}
~~~