using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// Znacznik na obiekcie płaszczyzny w scenie. SdfBaker szuka go po komponencie,
    /// nie po nazwie obiektu - zmiana nazwy w Hierarchy nie psuje wtedy bake'u.
    /// Sam nie robi nic w runtime, jest czystym markerem do wyszukania w Editorze.
    /// </summary>
    public class SdfPlaneMarker : MonoBehaviour
    {
    }
}
