using UnityEngine;

public class DatabaseManager : MonoBehaviour
{
    public static DatabaseManager Instance { get; private set; }

    [Header("ScriptableObject Databases")]
    public ItemDatabaseSO itemDatabase;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
            InitializeDatabases();
        }
        else
        {
            Destroy(gameObject); 
        }
    }

    private void InitializeDatabases()
    {
        if (itemDatabase != null)
        {
            itemDatabase.Initialize();
            Debug.Log("ItemDatabase initialized!");
        }
    }
}
