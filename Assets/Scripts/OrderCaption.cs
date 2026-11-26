using System;
using UnityEngine;
using UnityEngine.UI;

public class OrderCaption : MonoBehaviour
{
    public static OrderCaption Instance { get; private set; }

    public GameObject panel;
    public Text captionText;
    public Button okButton;

    Action onConfirmed;

    void Awake()
    {
        Instance = this;
        panel.SetActive(false);
        okButton.onClick.AddListener(Confirm);
    }

    public void Show(string text, Action confirmed)
    {
        captionText.text = text;
        onConfirmed = confirmed;
        panel.SetActive(true);
        PlayerController.SetUIOpen(true);
    }

    public void Confirm()
    {
        panel.SetActive(false);
        PlayerController.SetUIOpen(false);
        onConfirmed?.Invoke();
        onConfirmed = null;
    }
}
