using UnityEngine;
using UnityEngine.UI;

public class DebugCameraTester : MonoBehaviour
{
    public Button boardAnchorButton;
    public Button startAnchorButton;
    public Button lairOuterAnchorButton;
    public Button lairInnerAnchorButton;
    public Button lairAltarAnchorButton;
    public Button lairDungeonAnchorButton;

    public PawnView testPawnView;
    public Rank testPawnRank;
    public Team testPawnTeam;
    public bool testPawnIsSelected;
    void Start()
    {
        boardAnchorButton.onClick.AddListener(() =>
        {
            GameManager.instance.cameraManager.MoveCameraTo(Area.BOARD, false);
        });
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
        testPawnRank = Rank.THRONE;
        testPawnTeam = Team.RED;
        UpdateTestPawn();
    }

    void UpdateTestPawn()
    {
        testPawnView.TestSetSprite(testPawnRank, testPawnTeam);
    }

    void UpdateTestPawnAnimationState()
    {
        testPawnView.TestSpriteSelectTransition(testPawnIsSelected);
    }

    void UpdateTestPawnAnimationStateHurt()
    {
        testPawnView.HurtAnimation();
    }
}
