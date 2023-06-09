﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// AssetBundle编辑
/// </summary>
public class AssetBundleEditor
{
    #region 自动做标记

    //思路
    //1.找到资源保存的文件夹
    //2.遍历里面的每个场景文件夹
    //3.遍历场景文件夹里的所有文件系统
    //4.如果访问的是文件夹：再继续访问里面的所有文件系统，直到找到 文件 （递归）
    //5.找到文件 就要修改他的 assetbundle labels
    //6.用 AssetImporter 类 修改名称和后缀
    //7.保存对应的 文件夹名 和 具体路径

    [MenuItem("AssetBundle/Set AssetBundle Labels")]
    public static void SetAssetBundleLabels()
    {
        //移除所有没有使用的标记
        AssetDatabase.RemoveUnusedAssetBundleNames();

        //1.找到资源保存的文件夹
        string assetDirectory = Application.dataPath + "/Res";
        //Debug.Log(assetDirectory);

        DirectoryInfo directoryInfo = new DirectoryInfo(assetDirectory);
        DirectoryInfo[] sceneDirectories = directoryInfo.GetDirectories();
        //2.遍历里面的每个场景文件夹
        foreach (DirectoryInfo tmpDirectoryInfo in sceneDirectories)
        {
            string sceneDirectory = assetDirectory + "/" + tmpDirectoryInfo.Name;
            DirectoryInfo sceneDirectoryInfo = new DirectoryInfo(sceneDirectory);
            //错误检测
            if (sceneDirectoryInfo == null)
            {
                Debug.LogError(sceneDirectory + " 不存在!");
                return;
            }
            else
            {
                Dictionary<string, string> namePahtDict = new Dictionary<string, string>();

                //3.遍历场景文件夹里的所有文件系统
                //sceneDirectory
                //C:\Users\张晋枭\Documents\ABLesson\Assets\AssetBundles\Res\Scene1

                //C:/Users/张晋枭/Documents/ABLesson/Assets/AssetBundles/Res/Scene1
                int index = sceneDirectory.LastIndexOf("/");
                string sceneName = sceneDirectory.Substring(index + 1);
                onSceneFileSystemInfo(sceneDirectoryInfo, sceneName, namePahtDict);

                onWriteConfig(sceneName, namePahtDict);
            }
        }

        AssetDatabase.Refresh();

        Debug.Log("设置成功");
    }

    /// <summary>
    /// 记录配置文件
    /// </summary>
    private static void onWriteConfig(string sceneName, Dictionary<string, string> namePathDict)
    {
        string path = PathUtil.GetAssetBundleOutPath() + "/" + sceneName + "Record.txt";
        // Debug.Log(path);

        using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
        {
            //写二进制
            using (StreamWriter sw = new StreamWriter(fs))
            {
                sw.WriteLine(namePathDict.Count);

                foreach (KeyValuePair<string, string> kv in namePathDict)
                    sw.WriteLine(kv.Key + " " + kv.Value);
            }
        }
    }

    /// <summary>
    /// 遍历场景文件夹里的所有文件系统
    /// </summary>
    private static void onSceneFileSystemInfo(FileSystemInfo fileSystemInfo, string sceneName, Dictionary<string, string> namePahtDict)
    {
        if (!fileSystemInfo.Exists)
        {
            Debug.LogError(fileSystemInfo.FullName + " 不存在!");
            return;
        }

        DirectoryInfo directoryInfo = fileSystemInfo as DirectoryInfo;
        FileSystemInfo[] fileSystemInfos = directoryInfo.GetFileSystemInfos();
        foreach (var tmpFileSystemInfo in fileSystemInfos)
        {
            FileInfo fileInfo = tmpFileSystemInfo as FileInfo;
            if (fileInfo == null)
            {
                //代表强转失败，不是文件 就是文件夹
                //如果访问的是文件夹：再继续访问里面的所有文件系统，直到找到 文件 （递归）
                onSceneFileSystemInfo(tmpFileSystemInfo, sceneName, namePahtDict);
            }
            else
            {
                //就是文件
                //5.找到文件 就要修改他的 assetbundle labels
                setLabels(fileInfo, sceneName, namePahtDict);
            }
        }
    }

    /// <summary>
    /// 修改资源文件的 assetbundle labels
    /// </summary>
    private static void setLabels(FileInfo fileInfo, string sceneName, Dictionary<string, string> namePahtDict)
    {
        //对unity自身生成的meta文件 无视它
        if (fileInfo.Extension == ".meta")
            return;

        string bundleName = getBundleName(fileInfo, sceneName);
        //C:\Users\张晋枭\Documents\ABLesson\Assets\Res\Scene1\Buildings\Folder\Building4.prefab
        int index = fileInfo.FullName.IndexOf("Assets");
        //Assets\Res\Scene1\Buildings\Folder\Building4.prefab
        string assetPath = fileInfo.FullName.Substring(index);

        AssetImporter assetImporter = AssetImporter.GetAtPath(assetPath);
        //用 AssetImporter 类 修改名称和后缀
        assetImporter.assetBundleName = bundleName.ToLower();
        if (fileInfo.Extension == ".unity")
            assetImporter.assetBundleVariant = "u3d";
        else
            assetImporter.assetBundleVariant = "assetbundle";

        string folderName = "";
        //添加到字典里
        if (bundleName.Contains("/"))
            folderName = bundleName.Split('/')[1];
        else
            folderName = bundleName;

        string bundlePath = assetImporter.assetBundleName + "." + assetImporter.assetBundleVariant;
        if (!namePahtDict.ContainsKey(folderName))
            namePahtDict.Add(folderName, bundlePath);
    }

    /// <summary>
    /// 获取包名
    /// </summary>
    private static string getBundleName(FileInfo fileInfo, string sceneName)
    {
        string windowsPath = fileInfo.FullName;//C:\Users\张晋枭\Documents\ABLesson\Assets\Res\Scene1\Buildings\Folder\Building4.prefab
        //转换成unity可识别的路径
        string unityPath = windowsPath.Replace(@"\", "/");

        //C:/Users/张晋枭/Documents/ABLesson/Assets/Res/Scene1 /Buildings/Folder/Building4.prefab
        //C: \Users\张晋枭\Documents\ABLesson\Assets\Res\Scene1\Scene1.unity
        int index = unityPath.IndexOf(sceneName) + sceneName.Length;

        string bundlePath = unityPath.Substring(index + 1);

        if (bundlePath.Contains("/"))
        {
            //Buildings/Folder/Folder/Folder/Folder/Folder/Building4.prefab
            string[] tmp = bundlePath.Split('/');
            return sceneName + "/" + tmp[0];
        }
        else
        {
            //Scene1.unity
            return sceneName;
        }
    }

    #endregion 自动做标记

    #region 打包

    [MenuItem("AssetBundle/Build AssetBundles")]
    private static void BuildAllAssetBundles()
    {
        string outPath = PathUtil.GetAssetBundleOutPath();

        BuildPipeline.BuildAssetBundles(outPath, 0, BuildTarget.StandaloneWindows64);

        // 对所有文件
    }

    #endregion 打包

    #region 一键删除

    [MenuItem("AssetBundle/Delete All")]
    private static void DeleteAssetBundle()
    {
        string outPath = PathUtil.GetAssetBundleOutPath();

        Directory.Delete(outPath, true);
        File.Delete(outPath + ".meta");

        AssetDatabase.Refresh();
    }

    #endregion 一键删除

    [MenuItem("Tools/Create Files")]
    private static void CraeteFiles()
    {
        string outPath = PathUtil.GetAssetBundleOutPath();
        // 效验文件路径
        string filePath = outPath + "/files.txt";
        if (File.Exists(filePath)) File.Delete(filePath);

        // 遍历这个文件的下面所有文件
        List<string> fileList = new List<string>();
        GetFiles(new DirectoryInfo(outPath), ref fileList);

        FileStream fs = new FileStream(filePath, FileMode.CreateNew);
        StreamWriter sw = new StreamWriter(fs);

        for (int i = 0; i < fileList.Count; i++)
        {
            string file = fileList[i];
            string ext = Path.GetExtension(file);
            if (ext.EndsWith(".meta")) continue;

            // 生成文件名和md5
            string md5 = GetFileMd5(file);
            string value = file.Replace(outPath + "/", string.Empty);

            // 写入文件
            sw.WriteLine(value + "|" + md5);
        }
        sw.Close();
        fs.Close();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 遍历文件夹下的所有文件
    /// </summary>
    private static void GetFiles(FileSystemInfo fileSystemInfo, ref List<string> fileList)
    {
        DirectoryInfo directoryInfo = fileSystemInfo as DirectoryInfo;
        // 获取所有文件系统
        FileSystemInfo[] files = directoryInfo.GetFileSystemInfos();

        foreach (FileSystemInfo file in files)
        {
            FileInfo fileInfo = file as FileInfo;
            if (fileInfo != null) fileList.Add(fileInfo.FullName.Replace("\\", "/")); // 是文件
            else GetFiles(file, ref fileList); // 是文件夹 继续递归直到是文件
        }
    }

    /// <summary>
    /// 获取文件md5
    /// </summary>
    private static string GetFileMd5(string filePath)
    {
        FileStream fs = new FileStream(filePath, FileMode.Open);
        MD5 md5 = new MD5CryptoServiceProvider();

        byte[] result = md5.ComputeHash(fs);
        fs.Close();

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < result.Length; i++)
        {
            sb.Append(result[i].ToString("x2"));
        }
        return sb.ToString();
    }
}