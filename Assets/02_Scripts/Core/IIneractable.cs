using UnityEngine;
namespace NotMyBlood.Core
{
    public interface IInteractable
    {
        string GetInteractionPrompt();
        void Interact(GameObject interactor);
    }
}