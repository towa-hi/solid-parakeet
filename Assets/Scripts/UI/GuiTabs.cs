using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;


public class GuiTabs : MonoBehaviour
{
    public Color activeTabColor;
    public Color inactiveTabColor;
    public List<GuiTab> tabs = new List<GuiTab>();
    public int activeTab = 0;

    public event Action<int> OnActiveTabChanged;
    void Awake()
    {
        foreach (var tab in tabs)
        {
            tab.button.onClick.AddListener(() => SetActiveTab(tabs.IndexOf(tab)));
        }
        SetActiveTab(activeTab);
    }

    public void SetActiveTab(int index)
    {
        Debug.Log($"GuiTabs: Setting active tab to {index}");
        Debug.Log($"GuiTabs: Tabs count: {tabs.Count}");
        foreach (var tab in tabs)
        {
            Debug.Log($"GuiTabs: Setting tab {tab.name} to inactive");
            tab.tabBackground.color = inactiveTabColor;
            tab.bodyBackground.color = inactiveTabColor;
            tab.body.SetActive(false);
            tab.button.interactable = true;
        }
        var selectedTab = tabs[index];
        selectedTab.tabBackground.color = activeTabColor;
        selectedTab.bodyBackground.color = activeTabColor;
        selectedTab.body.SetActive(true);
        selectedTab.button.interactable = false;
        activeTab = index;
        OnActiveTabChanged?.Invoke(index);
    }
}