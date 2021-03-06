using System.Threading;
using Engine.Player;
using Menus;
using UnityEngine;
using Utility;

namespace Engine
{
    
    // The states a feature like network or VR can be in
    public enum FeatureState
    {
        offline = 0,
        starting = 1,
        online = 2,
        failed = 3,
        stopping = 4
    }

    public class GameLoader : MonoBehaviour
    {
        public static GameLoader instance;
    
        private void Awake()
        {
            if (instance == null) { instance = this; }
            else { Destroy(gameObject); return; }

            loadEvent.Register(Load);
            unloadEvent.Register(Unload);
            loadingDone.Register(LoadingDone);
        }

        [SerializeField] private PublicEventBool loadEvent;
        [SerializeField] private PublicEvent unloadEvent;
        [SerializeField] private PublicBool isHost;
        [SerializeField] private PublicByte userId;
    
        [SerializeField] private PublicEvent loadingDone;
        [SerializeField] private float timeOutLength = 30;
        [SerializeField] private PublicEvent loadingFailed;
        public FeatureSettings featureSettings;
    
        // Load initializes all Systems 
        private void Load(bool b)
        {
            isHost.value = b;
            userId.value = b ? (byte)0 : (byte)1;
            Debug.Log("Loading");
        
            StartUIController.instance.Get();
            
            for (var i = 0; i < featureSettings.features.Length; i++)
            {
                FeatureSettings.Feature feature = featureSettings.features[i];
                if (!feature.active)
                {
                    featureSettings.features[i].featureState.value = (int) FeatureState.failed;
                    continue;
                }

                // Features will be started on differnet thread to avoid any lag.
                int i1 = i;
                Threader.RunAsync(() =>
                {
                    if (WaitForDependencies(feature))
                    {
                        Threader.RunOnMainThread(() => {
                        
                            featureSettings.features[i1].startTime = Time.time;
                            feature.startEvent.Raise();
                        });
                    }
                    else
                    {
                        featureSettings.features[i1].featureState.value = (int) FeatureState.failed;
                    }
                });
            }

            WaitForLoading();
        }
    
        // WaitForDependencies runs till all feature that are marked as a dependency are done 
        private bool WaitForDependencies(FeatureSettings.Feature feature)
        {
            bool waiting = true;
            while (waiting)
            {
                waiting = false;
                foreach (string dependicy in feature.dependicies)
                {
                    if (featureSettings.Get(dependicy).featureState.value != (int) FeatureState.online)
                    {
                        waiting = true;
                    }
                    else if (featureSettings.Get(dependicy).featureState.value == (int) FeatureState.failed)
                    {
                        return false;
                    }
                }
                Thread.Sleep(100);
            }
            return true;
        }
    
        // Unload stop all features and systems so the game can return into the start screen.
        private void Unload()
        {
            Debug.Log("Unloading");
            foreach (FeatureSettings.Feature feature in featureSettings.features)
            {
                feature.stopEvent.Raise();
            }
        
            if (isVr.value)
            {
                cinemachineCam.SetActive(true);
                PcEventSystem.SetActive(true);
            }
        }
    
        // WaitForLoading wait for a feture to load in an different thread.
        private void WaitForLoading()
        {
            Threader.RunAsync(() =>
            {
                bool loading = true;
                while (loading)
                {
                    Threader.RunOnMainThread(() =>
                    {
                        bool failed = false;
                        bool done = true;

                        foreach (FeatureSettings.Feature feature in featureSettings.features)
                        {
                            // Set feature to failed if still loading wehn over timeout time.
                            if (feature.featureState.value == (int) FeatureState.starting && 
                                feature.startTime + feature.TimeOutTime < Time.time)
                            {
                                feature.featureState.value = (int) FeatureState.failed;
                            }

                            // Set done if all features = online or failed
                            if (feature.featureState.value != (int) FeatureState.online && 
                                feature.featureState.value != (int) FeatureState.failed)
                            {
                                done = false;
                            }
                        
                            // Set failed if an essential Feature failed
                            if (feature.featureState.value == (int) FeatureState.failed &&
                                feature.essential)
                            {
                                Debug.Log("Essential feature " + feature.name + " failed.");
                                failed = true;
                            }
                        }

                        if (failed)
                        {
                            Unload();
                            loadingFailed.Raise();
                            loading = false;
                        }else if (done)
                        {
                            loadingDone.Raise();
                            loading = false;
                        }
                    });
                    Thread.Sleep(1000);
                }
            });
        }


        [SerializeField] private PublicBool isVr;
        [SerializeField] private GameObject cinemachineCam;
        [SerializeField] private GameObject PcEventSystem;
        private void LoadingDone()
        {
            if (isVr.value)
            {
                cinemachineCam.SetActive(false);
                PcEventSystem.SetActive(false);
            }

            Network.NetworkController.instance.networkSend.FeatureSettingsToAllExceptOrigen(true);
        }
    }
}