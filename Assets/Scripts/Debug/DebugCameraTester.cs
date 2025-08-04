using UnityEngine;
using UnityEngine.UI;

public class DebugCameraTester : MonoBehaviour
{
    public Button startAnchorButton;
    public Button lairOuterAnchorButton;
    public Button lairInnerAnchorButton;
    public Button lairAltarAnchorButton;
    public Button lairDungeonAnchorButton;

    void Start()
    {
        startAnchorButton.onClick.AddListener(() =>
        {
            GameManager.instance.cameraManager.MoveCameraTo(Area.OUTSIDE, false);
        });
        lairOuterAnchorButton.onClick.AddListener(() =>
        {
            GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_OUTER, false);
        });
        lairInnerAnchorButton.onClick.AddListener(() =>
        {
            GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_INNER, false);
        });
        lairAltarAnchorButton.onClick.AddListener(() =>
        {
            GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_ALTAR, false);
        });
        lairDungeonAnchorButton.onClick.AddListener(() =>
        {
            GameManager.instance.cameraManager.MoveCameraTo(Area.LAIR_DUNGEON, false);
        });
    }
}
