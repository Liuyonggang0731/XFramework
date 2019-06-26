using UnityEngine;
using XFramework;
using XFramework.Pool;
using XFramework.Tasks;
using XFramework.Net;

/// <summary>
/// 这个类挂在初始场景中,是整个游戏的入口
/// </summary>
public class Game : MonoBehaviour
{
    // 框架模块
    // Start0
    public static FsmManager FsmModule { get; private set; }
    public static GraphicsManager GraphicsModule { get; private set; }
    public static MessengerManager MessengerModule { get; private set; }
    public static DataSubjectManager ObserverModule { get; private set; }
    public static ProcedureManager ProcedureModule { get; private set; }
    public static ResourceManager ResModule { get; private set; }
    public static TaskManager TaskModule { get; private set; }
    public static GameObjectPoolManager GameObjectPool { get; private set; }
    public static ObjectPoolManager ObjectPool { get; private set; }
    public static NetManager NetModule { get; private set; }
    // End0

    // 框架扩展模块
    // Start1
    public static MeshManager MeshModule { get; private set; }
    public static UIHelper UIModule { get; private set; }
    // End1

    // 初始流程
    public string TypeName;

    void Awake()
    {
        if (GameObject.FindObjectsOfType<Game>().Length > 1)
        {
            DestroyImmediate(this);
            return;
        }

        InitAllModel();
        Refresh();

        // 设置运行形后第一个进入的流程
        System.Type type = System.Type.GetType(TypeName);
        if (type != null)
        {
            ProcedureModule.StartProcedure(type);

            ProcedureBase procedure = ProcedureModule.GetCurrentProcedure();
            DeSerialize(procedure);
        }
        else
            Debug.LogError("当前工程还没有任何流程");

        DontDestroyOnLoad(this);
    }

    void Update()
    {
        GameEntry.ModuleUpdate(Time.deltaTime, Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 初始化模块，这个应该放再各个流程中，暂时默认开始时初始化所有模块
    /// </summary>
    public void InitAllModel()
    {
        // Start2
        FsmModule = GameEntry.AddModule<FsmManager>();
        GraphicsModule = GameEntry.AddModule<GraphicsManager>();
        MessengerModule = GameEntry.AddModule<MessengerManager>();
        ObserverModule = GameEntry.AddModule<DataSubjectManager>();
        ProcedureModule = GameEntry.AddModule<ProcedureManager>();
        ResModule = GameEntry.AddModule<ResourceManager>();
        TaskModule = GameEntry.AddModule<TaskManager>();
        GameObjectPool = GameEntry.AddModule<GameObjectPoolManager>();
        ObjectPool = GameEntry.AddModule<ObjectPoolManager>();
        NetModule = GameEntry.AddModule<NetManager>();
        MeshModule = GameEntry.AddModule<MeshManager>();
        UIModule = GameEntry.AddModule<UIHelper>();
        // End2
    }

    /// <summary>
    /// 刷新静态引用
    /// </summary>
    public void Refresh()
    {
        // Start3
        FsmModule = GameEntry.GetModule<FsmManager>();
        GraphicsModule = GameEntry.GetModule<GraphicsManager>();
        MessengerModule = GameEntry.GetModule<MessengerManager>();
        ObserverModule = GameEntry.GetModule<DataSubjectManager>();
        ProcedureModule = GameEntry.GetModule<ProcedureManager>();
        ResModule = GameEntry.GetModule<ResourceManager>();
        TaskModule = GameEntry.GetModule<TaskManager>();
        GameObjectPool = GameEntry.GetModule<GameObjectPoolManager>();
        ObjectPool = GameEntry.GetModule<ObjectPoolManager>();
        NetModule = GameEntry.GetModule<NetManager>();
        MeshModule = GameEntry.GetModule<MeshManager>();
        UIModule = GameEntry.GetModule<UIHelper>();
        // End3
    }

    public static void ShutdownModule<T>() where T : IGameModule
    {
        GameEntry.ShutdownModule<T>();
    }

    public static void StartModule<T>() where T : IGameModule
    {
        GameEntry.AddModule<T>();
    }

    /// <summary>
    /// 根据存储的byte数值给流程赋值
    /// </summary>
    /// <param name="procedure"></param>
    public void DeSerialize(ProcedureBase procedure)
    {
        System.Type type = procedure.GetType();
        string path = Application.persistentDataPath + "/" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "Procedure/" + type.Name;
        if (!System.IO.File.Exists(path))
            return;

        ProtocolBytes p = new ProtocolBytes(System.IO.File.ReadAllBytes(path));

        if (p.GetString() != type.Name)
        {
            Debug.LogError("类型不匹配");
            return;
        }

        p.DeSerialize(procedure);
    }
}
