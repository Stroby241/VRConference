using System.Collections.Generic;
using Network.FileShare;
using TMPro;
using UnityEngine;

namespace Engine.AudioSpeaker
{
    public class AudioSpeakerSelecter : MonoBehaviour
    {
        [SerializeField] private AudioSpeaker speaker;

        [SerializeField] private TMP_Dropdown dropdown;

        private void Update()
        {
            var options = new List<TMP_Dropdown.OptionData>();

            foreach (FileEntry fileEntry in FileShare.instance.fileEntries)
            {
                if (fileEntry.local)
                {
                    var option = new TMP_Dropdown.OptionData();
                    option.text = fileEntry.fileName;
                    options.Add(option);
                }
            }

            dropdown.options = options;
        }

        public void Play()
        {
            if (dropdown.options.Count > 0)
            {
                speaker.SetSong(dropdown.options[dropdown.value].text);
            }
        }
    }
}