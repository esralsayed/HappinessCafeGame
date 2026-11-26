using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public GameObject overviewCam;
    public GameObject closeupCam;
    public float closeupTime = 2f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            overviewCam.SetActive(!overviewCam.activeSelf);
    }

    public void ShowCloseup()
    {
        closeupCam.SetActive(true);
        CancelInvoke(nameof(HideCloseup));
        Invoke(nameof(HideCloseup), closeupTime);
    }

    void HideCloseup() { closeupCam.SetActive(false); }
}