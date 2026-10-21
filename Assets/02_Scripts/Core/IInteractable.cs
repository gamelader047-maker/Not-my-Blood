using UnityEngine;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Schnittstelle für interagierbare Objekte in der Spielwelt.
    /// </summary>
    public interface IInteractable
    {
        string GetInteractionPrompt();
        void Interact(GameObject interactor);
    }
}