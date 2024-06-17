using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace SisyphusFramework.Popup
{
    public partial class PopupManager : BaseSingleton<PopupManager>
    {

        private struct PopupResourcesModel
        {
            public string Name;
            public PackedScene SceneResource;
        }

        private struct PopupModel
        {
            public string Name;
            public Node SceneNode;
        }

        [Export]
        private const string _folderPath = "res://assets/_tcu/resources/popup/";

        [Export]
        private const CanvasLayer popupsContainer = null;

        private List<PopupModel> _cachedPopups = new List<PopupModel>();
        private Queue<string> _queuePopupNames = new Queue<string>();

        private ConcurrentBag<PopupResourcesModel> _popupDatabases = new ConcurrentBag<PopupResourcesModel>(); //new Lis();
        public override void _Ready()
        {
            LoadPopupsFromFolder(_folderPath);
        }

        #region Setup
        private void LoadPopupsFromFolder(string folderPath)
        {
            string[] sceneFiles = System.IO.Directory.GetFiles(folderPath, "*.tscn");

            Parallel.ForEach(sceneFiles, scenePath =>
            {
                var scene = ResourceLoader.Load<PackedScene>(scenePath);
                var name = RemovePopupSuffix(scenePath);
                _popupDatabases.Add(new PopupResourcesModel
                {
                    Name = name,
                    SceneResource = scene
                });

#if LOG_OUTPUT_INFO
            GD.Print("Loaded popup: " + scenePath);
#endif
            });
        }



        #endregion


        #region Main
        public bool OpenPopup(string popupName, object param = null)
        {
            var lastestPopup = _queuePopupNames.Peek();
            if (popupName == lastestPopup)
                return false;
            if (lastestPopup != null)
                Hide(lastestPopup);
            _queuePopupNames.Enqueue(popupName);
            if (!IsCached(popupName))
                CreatePopupInstance(popupName);

            Show(popupName, param);
            return true;
        }

        public void ClosePopup()
        {
            if (_queuePopupNames.Count <= 0)
                return;
            var lastestPopup = _queuePopupNames.Dequeue();
            Hide(lastestPopup);
        }
        public void ClosePopup(string popupName){
            if (_queuePopupNames.Count <= 0)
                return;
            if (_queuePopupNames.Peek() == popupName)
                _queuePopupNames.Dequeue();
            Hide(popupName);
        }

        public void CloseAllPopups()
        {
            if (_queuePopupNames.Count <= 0)
                return;
            while (_queuePopupNames.Count > 0)
            {
                var lastestPopup = _queuePopupNames.Dequeue();
                Hide(lastestPopup);
            }
        }

        #endregion

        #region Prive
        Node CreatePopupInstance(string popupName)
        {
            var popup_scene_resources = GetPopupFromDB(popupName: popupName);
            if (popup_scene_resources == null)
            {
#if LOG_OUTPUT_ERROR
                        GD.PrintError("Popup not found: " + popupName);
#endif
                return null;
            }
            Node popupInstance = popup_scene_resources.Instantiate();
            _cachedPopups.Add(new PopupModel
            {
                Name = popupName,
                SceneNode = popupInstance
            });
            popupsContainer.AddChild(popupInstance);
            PopupBase popupBase = popupInstance as PopupBase;
            popupBase.Setup(new PopupBase.PopupParam {
                Manager = this,
                PopupName = popupName
            });
            return popupInstance;
        }

        void Show(string popupName, object param = null)
        {
            var cachePopup = GetPopupFromCache(popupName);
            if (cachePopup == null)
                return;

            cachePopup.ProcessMode = ProcessModeEnum.Always;

            var popupBase = cachePopup as PopupBase;
            popupBase.OnShowing(param);
            popupBase.Visible = true;
        }
        void Hide(string popupName)
        {
            if (!HasAnyActivePopup)
                return;
            var node = GetPopupFromCache(popupName);
            if (node == null)
                return;
            node.ProcessMode = ProcessModeEnum.Disabled;

            var popupBase = node as PopupBase;
            popupBase.Visible = false;
        }

        bool IsLastest(string popupName)
        {
            if (!HasAnyActivePopup)
                return false;

            return _queuePopupNames.Peek() == popupName;
        }

        bool IsCached(string popupName)
        {
            if (!HasAnyActivePopup)
                return false;
            return _cachedPopups.Any((e)=> {
                return e.Name == popupName;
            });
        }

        Node GetPopupFromCache(string popupName)
        {
            var cache = _cachedPopups.Find(x => x.Name == popupName);
            return cache.SceneNode;
        }
        PackedScene GetPopupFromDB(string popupName)
        {
            foreach (var i in _popupDatabases)
            {
                if (i.Name == popupName)
                {
                    return i.SceneResource;
                }
            }
            return null;
        }

        bool HasAnyActivePopup { get => _queuePopupNames.Count > 0; }
        #endregion


        #region  Utils

        private string RemovePopupSuffix(string popupName)
        {
            int index = popupName.LastIndexOf("Popup.tscn");
            if (index > 0)
            {
                return popupName.Substring(0, index);
            }
            return popupName;
        }

        #endregion

    }
}
