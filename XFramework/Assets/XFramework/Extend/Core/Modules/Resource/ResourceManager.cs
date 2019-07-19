﻿#define ABTests
#if!UNITY_EDITOR || ABTest
#define AB
#endif

using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using XFramework.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XFramework
{
    /// <summary>
    /// 资源管理器
    /// </summary>
    public partial class ResourceManager : IGameModule
    {
        public readonly string ABPath = Application.streamingAssetsPath + "/AssetBundles";

        /// <summary>
        /// AB包的缓存
        /// </summary>
        private Dictionary<string, AssetBundle> m_ABDic;
        /// <summary>
        /// 用于获取AB包的依赖关系
        /// </summary>
        private AssetBundleManifest m_Mainfest;

        /// <summary>
        /// 资源加载方式
        /// </summary>
        private ResMode m_ResMode;
        public ResMode ResMode { get { return m_ResMode; } }

        public ResourceManager()
        {
            m_ABDic = new Dictionary<string, AssetBundle>();
#if AB
            m_ResMode = ResMode.AssetBundle;

            AssetBundle m_MainfestAB = AssetBundle.LoadFromFile(ABPath + "/AssetBundles");
            if (m_MainfestAB != null)
                m_Mainfest = m_MainfestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
# else
            m_ResMode = ResMode.AssetDataBase;
#endif
        }

        #region 资源加载

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public T Load<T>(string path) where T : Object
        {
            
            if (path.Substring(0, 4) == "Res/")
            {
                path = path.Substring(4, path.Length - 4);
                return Resources.Load<T>(path);
            }
#if AB
            var temp = Utility.Text.SplitPathName(path);
            return GetAssetBundle(temp[0]).LoadAsset<T>(temp[1]);
#else
            return LoadWithADB<T>(path);
#endif
        }

        /// <summary>
        /// 加载一个路径下的所有资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public T[] LoadAll<T>(string path) where T : Object
        {
            if (path.Substring(0, 4) == "Res/")
            {
                path = path.Substring(4, path.Length - 4);
                return Resources.LoadAll<T>(path);
            }

#if AB
            return GetAssetBundle(path).LoadAllAssets<T>();
#else
            return LoadAllWithADB<T>(path);
#endif
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <param name="callBack"></param>
        public IProgress LoadAsync<T>(string path, System.Action<T> callBack) where T : Object
        {
            if (path.Substring(0, 4) == "Res/")
            {
                path = path.Substring(4, path.Length - 4);
                var request = Resources.LoadAsync(path);

                // experiment
                SingleTask task = new SingleTask(() =>
                {
                    return request.isDone;
                });
                task.Then(() =>
                {
                    callBack.Invoke(request.asset as T);
                    return true;
                });
                GameEntry.GetModule<TaskManager>().StartTask(task);
                return null;
            }

            else
            {
#if AB
                var temp = Utility.Text.SplitPathName(path);

                // experiment
                return GetAssetBundleAsync(temp[0], (ab) =>
                {
                    var request = ab.LoadAssetAsync(temp[1]);
                    SingleTask task = new SingleTask(() => { return request.isDone; });
                    task.Then(() => { callBack(request.asset as T); return true; });
                    GameEntry.GetModule<TaskManager>().StartTask(task);
                });
#else
                T obj = LoadWithADB<T>(path);
                callBack(obj);
                return null;
#endif
            }

        }

#endregion

#region AB包加载

        /// <summary>
        /// 获取一个AB包文件
        /// </summary>
        /// <param name="path">相对路径</param>
        /// <returns></returns>
        public AssetBundle GetAssetBundle(string path)
        {
            m_ABDic.TryGetValue(path, out AssetBundle ab);
            if (ab == null)
            {
                path = path.ToLower();
                string abName = string.IsNullOrEmpty(path) ? "" : "/" + path;

                ab = AssetBundle.LoadFromFile(ABPath + abName + ".ab");
                if (ab == null)
                    Debug.LogError(path + " 为空");
                m_ABDic.Add(path, ab);
            }

            //加载当前AB包的依赖包
            string[] dependencies = m_Mainfest.GetAllDependencies(ab.name);

            foreach (var item in dependencies)
            {
                string key = Name2Key(item);  // 对key去除.ab
                if (!m_ABDic.ContainsKey(key))
                {
                    AssetBundle dependAb = GetAssetBundle(key);
                    m_ABDic.Add(key, dependAb);
                }
            }

            return ab;
        }

        /// <summary>
        /// 异步获取AB包
        /// </summary>
        /// <param name="path"></param>
        public IProgress GetAssetBundleAsync(string path, System.Action<AssetBundle> callBack)
        {
            path = Path2Key(path);
            m_ABDic.TryGetValue(path, out AssetBundle ab);
            if (ab == null)
            {
                string abName = string.IsNullOrEmpty(path) ? "" : "/" + path;

                AssetBundleCreateRequest mainRequest = AssetBundle.LoadFromFileAsync(ABPath + abName + ".ab");
                // 添加任务列表
                List<AssetBundleCreateRequest> requests = new List<AssetBundleCreateRequest>
                {
                    mainRequest
                };

                string[] dependencies = m_Mainfest.GetAllDependencies(path + ".ab");
                foreach (var name in dependencies)
                {
                    if (m_ABDic.ContainsKey(Name2Key(name)))
                        continue;
                    requests.Add(AssetBundle.LoadFromFileAsync(ABPath + "/" + name));
                }

                // experiment
                Tasks.ITask[] tasks = new Tasks.ITask[requests.Count];

                for (int i = 0; i < tasks.Length; i++)
                {
                    int index = i;
                    tasks[index] = new SingleTask(() =>
                    {
                        return requests[index].isDone;
                    });
                    tasks[index].Then(new SingleTask(() =>
                    {
                        m_ABDic.Add(Name2Key(requests[index].assetBundle.name), requests[index].assetBundle);
                        return true;
                    }));
                }

                AllTask abTask = new AllTask(tasks);
                abTask.Then(new SingleTask(() =>
                {
                    callBack.Invoke(mainRequest.assetBundle);
                    return true;
                }));
                GameEntry.GetModule<TaskManager>().StartTask(abTask);

                return new ResProgress(requests.ToArray());
            }
            else
            {
                callBack(ab);
                return null;
            }
        }

        /// <summary>
        /// 卸载AB包
        /// </summary>
        /// <param name="path"></param>
        /// <param name="unLoadAllObjects"></param>
        public void UnLoad(string path, bool unLoadAllObjects = true)
        {
            GetAssetBundle(path).Unload(unLoadAllObjects);
            m_ABDic.Remove(path);
        }

#endregion

        private string Name2Key(string abName)
        {
            return abName.Substring(0, abName.Length - 3);
        }

        private string Path2Key(string path)
        {
            return path.ToLower();
        }


#if !AB

        /// <summary>
        /// 利用AssetDataBase加载资源
        /// </summary>
        private T LoadWithADB<T>(string path) where T : Object
        {
            string suffix = ""; // 后缀
            switch (typeof(T).Name)
            {
                case "GameObject":
                    suffix = ".prefab";
                    break;
                case "Material":
                    suffix = ".mat";
                    break;
                case "TerrainLayer":
                    suffix = ".terrainlayer";
                    break;
                case "Texture":
                case "Texture2D":
                case "Sprite":
                    return LoadPicture<T>(path);
            }
            return AssetDatabase.LoadAssetAtPath<T>(path + suffix);
        }

        /// <summary>
        /// 在没有后缀的情况下加载贴图
        /// </summary>
        /// <typeparam name="T">Texture,Texture2D</typeparam>
        private T LoadPicture<T>(string path) where T : Object
        {
            T texture;
            texture = AssetDatabase.LoadAssetAtPath<T>(path + ".png");
            if (!texture)
                texture = AssetDatabase.LoadAssetAtPath<T>(path + ".jpg");
            if (!texture)
                texture = AssetDatabase.LoadAssetAtPath<T>(path + ".tga");
            if (!texture)
                texture = AssetDatabase.LoadAssetAtPath<T>(path + ".psd");
            return texture;
        }

        /// <summary>
        /// 加载一个文件夹下的所有资源
        /// </summary>
        private T[] LoadAllWithADB<T>(string path) where T : Object
        {
            List<T> objs = new List<T>();
            System.IO.DirectoryInfo info = new System.IO.DirectoryInfo(Application.dataPath.Replace("Assets", "") + path);
            foreach (var item in info.GetFiles("*",System.IO.SearchOption.TopDirectoryOnly))
            {
                if (item.Name.EndsWith(".meta"))
                    continue;
                string assetPath = path + "/" + item.Name.Split('.')[0];
                objs.Add(LoadWithADB<T>(assetPath));
            }
            return objs.ToArray();
        }

#endif

#region 接口实现

        public int Priority { get { return 100; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            //m_TaskPool.Update();
        }

        public void Shutdown()
        {
            m_ABDic.Clear();
            m_Mainfest = null;
            AssetBundle.UnloadAllAssetBundles(true);
        }

#endregion
    }

    public enum ResMode
    {
        AssetBundle,
        AssetDataBase,
    }
}