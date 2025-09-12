using System;
using System.Collections.Generic;
using Contract;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ArenaController))]
public class ArenaControllerEditor : Editor
{
    enum TestOutcome
    {
        RedWins,
        BlueWins,
        BothLose,
    }

    static Rank redRank = Rank.GRUNT;
    static Rank blueRank = Rank.GRUNT;
    static TestOutcome outcome = TestOutcome.RedWins;
    static bool isHex = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Battle Test", EditorStyles.boldLabel);

        redRank = (Rank)EditorGUILayout.EnumPopup("Red Rank", redRank);
        blueRank = (Rank)EditorGUILayout.EnumPopup("Blue Rank", blueRank);
        outcome = (TestOutcome)EditorGUILayout.EnumPopup("Outcome", outcome);
        isHex = EditorGUILayout.Toggle("Hex Tiles", isHex);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Start Test Battle"))
            {
                StartTestBattle();
            }
        }
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to run the test battle.", MessageType.Info);
        }
    }

    void StartTestBattle()
    {
        ArenaController controller = (ArenaController)target;
        if (controller == null)
        {
            return;
        }

        controller.Initialize(isHex);

        // Hide all Canvases during editor-invoked test to avoid UI overlap
        foreach (var canvas in GameObject.FindObjectsOfType<Canvas>(true))
        {
            canvas.enabled = false;
        }

        PawnId redId = new PawnId(new Vector2Int(0, 0), Team.RED);
        PawnId blueId = new PawnId(new Vector2Int(1, 0), Team.BLUE);

        PawnState preRed = new PawnState
        {
            alive = true,
            moved = false,
            moved_scout = false,
            pawn_id = redId,
            pos = new Vector2Int(0, 0),
            rank = redRank,
            zz_revealed = true,
        };
        PawnState preBlue = new PawnState
        {
            alive = true,
            moved = false,
            moved_scout = false,
            pawn_id = blueId,
            pos = new Vector2Int(1, 0),
            rank = blueRank,
            zz_revealed = true,
        };

        bool redDies = outcome == TestOutcome.BlueWins || outcome == TestOutcome.BothLose;
        bool blueDies = outcome == TestOutcome.RedWins || outcome == TestOutcome.BothLose;

        PawnState postRed = preRed;
        postRed.alive = !redDies;
        PawnState postBlue = preBlue;
        postBlue.alive = !blueDies;

        SnapshotPawnDelta redDelta = new SnapshotPawnDelta(preRed, postRed);
        SnapshotPawnDelta blueDelta = new SnapshotPawnDelta(preBlue, postBlue);

        Dictionary<PawnId, SnapshotPawnDelta> pawnDeltas = new Dictionary<PawnId, SnapshotPawnDelta>
        {
            { redId, redDelta },
            { blueId, blueDelta },
        };

        List<PawnId> deadList = new List<PawnId>();
        if (redDies) deadList.Add(redId);
        if (blueDies) deadList.Add(blueId);

        BattleEvent battle = new BattleEvent
        {
            participants = new[] { redId, blueId },
            revealed = Array.Empty<PawnId>(),
            dead = deadList.ToArray(),
            winnerPos = new Vector2Int(0, 0),
            revealedRanks = Array.Empty<(PawnId pawn, Rank rank, bool wasHidden)>(),
        };

        TurnResolveDelta delta = new TurnResolveDelta
        {
            pawnDeltas = pawnDeltas,
            moves = new Dictionary<PawnId, MoveEvent>(),
            battles = new[] { battle },
        };

        controller.StartBattle(battle, delta);
    }
}


