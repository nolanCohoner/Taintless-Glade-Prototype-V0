using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Oriente l'objet parallèlement à l'écran : les persos 2D restent lisibles malgré la caméra inclinée.
    /// </summary>
    [ExecuteAlways]
    public class Billboard : MonoBehaviour
    {
        /// <summary>
        /// Recopie la rotation de la caméra principale après tous les déplacements de la frame.
        /// </summary>
        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;
        }
    }
}
