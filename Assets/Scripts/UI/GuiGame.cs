using System;
using UnityEngine;

public class GuiGame : GuiElement
{
    public GamePhase gamePhase;
    public GuiPawnSetup pawnSetup;
    public void InitializeSetup(SetupParameters inSetupParameters)
    {
        SetGamePhase(GamePhase.SETUP);
    }

    public void SetGamePhase(GamePhase phase)
    {
        switch (gamePhase)
        {
            case GamePhase.UNINITIALIZED:
                break;
            case GamePhase.SETUP:
                break;
            case GamePhase.MOVE:
                break;
            case GamePhase.RESOLVE:
                break;
            case GamePhase.END:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        gamePhase = phase;
        switch (gamePhase)
        {
            case GamePhase.UNINITIALIZED:
                pawnSetup.enabled = false;
                break;
            case GamePhase.SETUP:
                pawnSetup.enabled = true;
                pawnSetup.Initialize(GameManager.instance.boardManager.setupParameters, GameManager.instance.boardManager.pawnsLeft);
                break;
            case GamePhase.MOVE:
                pawnSetup.enabled = false;
                break;
            case GamePhase.RESOLVE:
                pawnSetup.enabled = false;
                break;
            case GamePhase.END:
                pawnSetup.enabled = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
