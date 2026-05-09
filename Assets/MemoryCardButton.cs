using UnityEngine;

public class MemoryCardButton : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private int cardIndex;

    public void OnClickFlip()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("MemoryCardButton sem referencia para GameManager.");
            return;
        }

        gameManager.OnCardClicked(cardIndex);
    }

    public void SetCardIndex(int newIndex)
    {
        cardIndex = newIndex;
    }

    public void SetGameManager(GameManager newGameManager)
    {
        gameManager = newGameManager;
    }
}
