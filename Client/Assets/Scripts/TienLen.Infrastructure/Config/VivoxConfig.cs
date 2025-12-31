using UnityEngine;

namespace TienLen.Infrastructure.Config
{
    [CreateAssetMenu(fileName = "VivoxConfig", menuName = "TienLen/Vivox Config", order = 0)]
    public class VivoxConfig : ScriptableObject
    {
        [SerializeField] private string _vivoxServer;
        [SerializeField] private string _vivoxDomain;
        [SerializeField] private string _vivoxIssuer;

        public string VivoxServer => _vivoxServer;
        public string VivoxDomain => _vivoxDomain;
        public string VivoxIssuer => _vivoxIssuer;
    }
}
