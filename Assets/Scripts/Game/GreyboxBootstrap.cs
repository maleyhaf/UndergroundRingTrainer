using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightTrainer.Game
{
    /// <summary>
    /// Zero-setup start: when you press Play in SampleScene (or a scene named "Greybox"),
    /// the greybox fight is created automatically. To use it in another scene, add the
    /// GreyboxGame component to any GameObject.
    /// </summary>
    public static class GreyboxBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (Object.FindAnyObjectByType<GreyboxGame>() != null) return;
            string scene = SceneManager.GetActiveScene().name;
            if (scene != "SampleScene" && scene != "Greybox") return;
            new GameObject("GreyboxGame").AddComponent<GreyboxGame>();
        }
    }
}
