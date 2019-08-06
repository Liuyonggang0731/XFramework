﻿using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using XFramework.Resource;
using System.IO;
using System;
using System.Text;
using System.Linq;

namespace XFramework.Editor
{
    public partial class AssetBundleEditor
    {
        private class Mainfest2Json : SubWindow
        {
            private List<string> m_Paths;
            private string m_OutPutPath;

            public override void OnEnable()
            {
                string temp = EditorPrefs.GetString("ABPaths", "");
                if (temp != "")
                {
                    m_Paths = Str2List(EditorPrefs.GetString("ABPaths", ""));
                }
                else
                {
                    m_Paths = new List<string>();
                }
                
                m_OutPutPath = EditorPrefs.GetString("DependencePath", Application.streamingAssetsPath);
            }

            public override void OnDisable()
            {
                EditorPrefs.SetString("ABPaths", List2Str(m_Paths));
                EditorPrefs.SetString("DependencePath", m_OutPutPath);
            }

            public override void OnGUI()
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("优先级越往下越高");
                    using (new EditorGUILayout.VerticalScope())
                    {
                        for (int i = 0; i < m_Paths.Count; i++)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.TextField(m_Paths[i]);
                                if (GUILayout.Button(EditorIcon.Trash))
                                {
                                    m_Paths.RemoveAt(i);
                                }
                            }
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(EditorIcon.Plus))
                            {
                                string temp = EditorUtility.OpenFilePanel("选中mainfest", Application.streamingAssetsPath, "");
                                Debug.Log(temp);
                                if (!(string.IsNullOrEmpty(temp) || m_Paths.Contains(temp)))
                                {
                                    m_Paths.Add(temp);
                                }
                            }
                            if (GUILayout.Button("Generate"))
                            {
                                GenerateJson();
                            }
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("输出路径");
                        GUILayout.TextField(m_OutPutPath);
                        if (GUILayout.Button(EditorIcon.Folder))
                        {
                            string temp = EditorUtility.OpenFolderPanel("输出文件夹", Application.streamingAssetsPath, "");
                            if (!string.IsNullOrEmpty(temp))
                            {
                                m_OutPutPath = temp;
                            }
                        }
                    }
                }
            }

            private void GenerateJson()
            {
                AssetBundle.UnloadAllAssetBundles(true);
                DependenciesData[] datas = new DependenciesData[m_Paths.Count];
                for (int i = 0; i < m_Paths.Count; i++)
                {
                    if (!m_Paths[i].EndsWith(".json"))
                    {
                        AssetBundle mainfestAB = AssetBundle.LoadFromFile(m_Paths[i]);
                        var mainfest = mainfestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                        string[] abNames = mainfest.GetAllAssetBundles();

                        List<SingleDepenciesData> singleDatas = new List<SingleDepenciesData>();

                        for (int j = 0; j < abNames.Length; j++)
                        {
                            var dpNames = mainfest.GetDirectDependencies(abNames[j]);
                            if (dpNames.Length <= 0)
                            {
                                continue;
                            }
                            singleDatas.Add(new SingleDepenciesData(abNames[j], dpNames));
                        }
                        datas[i] = new DependenciesData(singleDatas.ToArray());
                    }
                    else
                    {
                        string tempJson = System.IO.File.ReadAllText(m_Paths[i]);
                        datas[i] = JsonUtility.FromJson<DependenciesData>(tempJson);
                    }
                }


                string json = JsonUtility.ToJson(ConbineDependence(datas), true);
                File.WriteAllText(m_OutPutPath + "/depenencies.json", json);
                AssetDatabase.Refresh();
            }

            /// <summary>
            /// 融合依赖关系
            /// 在数组中越靠后优先级越高
            /// </summary>
            /// <param name="datas"></param>
            private DependenciesData ConbineDependence(DependenciesData[] datas)
            {
                List<SingleDepenciesData> singleDatas = new List<SingleDepenciesData>();

                foreach (var data in datas.Reverse())
                {
                    string[] assetBundles = data.GetAllAssetBundles();

                    foreach (var abName in assetBundles)
                    {
                        if (Contains(abName))
                            continue;
                        singleDatas.Add(new SingleDepenciesData(abName, data.GetDirectDependencies(abName)));
                    }
                    
                }

                return new DependenciesData(singleDatas.ToArray());

                bool Contains(string abName)
                {
                    foreach (var item in singleDatas)
                    {
                        if (abName == item.Name)
                            return true;
                    }
                    return false;
                }
            }

            private string List2Str(List<String> list)
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var item in list)
                {
                    stringBuilder.Append(item);
                    stringBuilder.Append(',');
                }
                return stringBuilder.ToString(0,stringBuilder.Length - 1);
            }

            private List<string> Str2List(string value)
            {
                string[] list = value.Split(',');
                return list.ToList();
            }
        }
    }
}
