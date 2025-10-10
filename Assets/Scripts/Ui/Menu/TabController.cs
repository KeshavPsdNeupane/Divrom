using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class Tab
{
    public Image tabImage;     
    public GameObject tabPage;  
}

public class TabController : MonoBehaviour
{
    [SerializeField] private Tab[] tabs;     
    [SerializeField] private Button[] buttons; 

    void Awake()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() => ActivateTab(index));
        }
    }

    void Start()
    {
        ActivateTab(0);
    }

    public void ActivateTab(int tabIndex)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            bool isActive = (i == tabIndex);

            if (tabs[i].tabImage != null)
                tabs[i].tabImage.color = isActive ? Color.white : Color.gray;

            if (tabs[i].tabPage != null)
                tabs[i].tabPage.SetActive(isActive);
            TMP_Text tmpText = tabs[i].tabPage?.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.color = isActive ? Color.white : Color.gray;
            }
        }
    }
}
