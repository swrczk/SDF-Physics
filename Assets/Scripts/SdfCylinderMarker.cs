using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Znacznik na obiekcie walca w scenie. SdfBaker czyta z jego Transformu
    /// pozycję (center), localScale.x jako radius (zakłada domyślny walec Unity
    /// o promieniu 0.5 przy scale=1) i localScale.y jako wysokość (domyślna
    /// wysokość walca Unity to 2 przy scale=1).
    /// Rotacja walca NIE jest uwzględniana - zakładamy, że stoi prosto (jak w scenie z zadania).
    /// </summary>
    public class SdfCylinderMarker : MonoBehaviour
    {
    }
}
