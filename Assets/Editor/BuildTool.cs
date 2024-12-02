using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Net;
using Debug = UnityEngine.Debug;

public class BuildTool : EditorWindow
{
    private string buildPath = "Builds/WebGL";
    private BuildTarget buildTarget = BuildTarget.WebGL;
    private bool connectProfiler = false;
    private bool enableDeepProfiling = false;
    private int profilerMaxMemory = 200000000;

    private HttpListener httpListener;

    [MenuItem("Tools/Build Tool")]
    public static void ShowWindow()
    {
        GetWindow<BuildTool>("Build Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("Build Settings", EditorStyles.boldLabel);

        buildPath = EditorGUILayout.TextField("Build Path", buildPath);
        buildTarget = (BuildTarget)EditorGUILayout.EnumPopup("Build Target", buildTarget);
        connectProfiler = EditorGUILayout.Toggle("Connect Profiler", connectProfiler);
        enableDeepProfiling = EditorGUILayout.Toggle("Enable Deep Profiling", enableDeepProfiling);
        profilerMaxMemory = EditorGUILayout.IntField("Profiler Max Memory", profilerMaxMemory);

        if (GUILayout.Button("Build"))
        {
            PerformBuild();
        }

        if (GUILayout.Button("Build and Run"))
        {
            PerformBuildAndRun();
        }
    }

    private void PerformBuild()
    {
        string[] scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);

        if (string.IsNullOrEmpty(buildPath))
        {
            Debug.LogError("Build path is empty!");
            return;
        }

        BuildOptions buildOptions = BuildOptions.Development;

        if (enableDeepProfiling)
        {
            buildOptions |= BuildOptions.EnableDeepProfilingSupport;
        }

        BuildPipeline.BuildPlayer(scenes, buildPath, buildTarget, buildOptions);
        Debug.Log($"Build completed at: {buildPath}");
    }

    private void PerformBuildAndRun()
    {
        PerformBuild();

        if (buildTarget == BuildTarget.WebGL)
        {
            StartLocalServer();
        }
        else
        {
            Debug.LogError("Build and Run is currently only supported for WebGL in this tool.");
        }
    }

    private void StartLocalServer()
    {
        string webGLPath = Path.GetFullPath(buildPath);

        if (!Directory.Exists(webGLPath))
        {
            Debug.LogError($"Build directory does not exist: {webGLPath}");
            return;
        }

        if (httpListener != null && httpListener.IsListening)
        {
            httpListener.Close();
        }

        httpListener = new HttpListener();
        httpListener.Prefixes.Add("http://localhost:8080/");
        httpListener.Start();

        Debug.Log($"Serving WebGL build from: {webGLPath}");
        Debug.Log("Server started at http://localhost:8080/");

        httpListener.BeginGetContext(result =>
        {
            var context = httpListener.EndGetContext(result);
            string filePath = Path.Combine(webGLPath, context.Request.Url.LocalPath.TrimStart('/'));

            if (File.Exists(filePath))
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                context.Response.ContentType = GetMimeType(filePath);
                context.Response.ContentLength64 = fileData.Length;
                context.Response.OutputStream.Write(fileData, 0, fileData.Length);
            }
            else
            {
                context.Response.StatusCode = 404;
            }

            context.Response.OutputStream.Close();
            httpListener.BeginGetContext(null, null); // Continue listening for requests
        }, null);

        Application.OpenURL("http://localhost:8080/");
    }

    private string GetMimeType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".html" => "text/html",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".wasm" => "application/wasm",
            ".data" => "application/octet-stream",
            _ => "application/octet-stream",
        };
    }
}
