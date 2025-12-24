using System.Collections.Generic;
using UnityEngine;

namespace TienLen.Infrastructure.Config
{
    [CreateAssetMenu(fileName = "AvatarRegistry", menuName = "TienLen/Config/AvatarRegistry")]
    public class AvatarRegistry : ScriptableObject
    {
        [SerializeField] private Sprite _defaultAvatar;
        [SerializeField] private List<Sprite> _avatars;

        public Sprite GetAvatar(int index)
        {
            if (_avatars == null || _avatars.Count == 0) return _defaultAvatar;
            if (index < 0 || index >= _avatars.Count) return _defaultAvatar;
            
            return _avatars[index] ?? _defaultAvatar;
        }
    }
}
