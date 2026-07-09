using UnityEngine;

public sealed class NightShiftInteractable : MonoBehaviour
{
    [SerializeField] private NightShiftGameController gameController;
    [SerializeField] private NightShiftInteractionAction action;
    [SerializeField] private string prompt = "Interact";

    public string Prompt => prompt;

    public void Configure(NightShiftGameController controller, NightShiftInteractionAction interactionAction, string interactionPrompt)
    {
        gameController = controller;
        action = interactionAction;
        prompt = interactionPrompt;
    }

    public void Interact()
    {
        if (gameController != null)
            gameController.UseInteraction(action);
    }
}
