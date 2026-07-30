using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using GradationBaker.Data;
using GradationBaker.Execute;
using GradationBaker.Localization;
using System.IO;
using System.Collections.Generic;
using GradationBaker;

namespace GradationBaker.UI
{
    public class GradationBakerWindow : EditorWindow
    {
        private const string UXML_GUID = "c2e5daad2fc96f52b2ae755fbe5cd4c3";
        private const string USS_GUID  = "b1d4c99c1fb85e41a198d64ead4bc3b2";

        [SerializeField]
        private GradationSettings _settings = new GradationSettings();

        private GradationBakingExecutor _baker;
        private GradationSceneHandle _sceneHandle;
        private GradationPreview _preview;

        // UI Toolkit Elements
        private VisualElement _rootElement;
        private Label _versionLabel;
        private Button _versionReloadButton;
        private Button _langButton;
        private Toggle _enableToolToggle;
        
        private VisualElement _toolDisabledCard;
        private VisualElement _mainContentContainer;
        private VisualElement _meshListContainer;
        private Button _createWorkMeshButton;
        private Button _clearAllButton;
        
        private GradientField _gradientField;
        private Button _saveGradientButton;
        private Button _loadGradientButton;
        
        private DropdownField _shapeDropdown;
        private Vector3Field _centerField;
        private Button _centerResetButton;
        private Vector3Field _rotationField;
        private Button _rotationResetButton;
        private FloatField _heightField;
        private Vector3Field _sizeField;
        private Label _centerLabel;
        private Label _rotationLabel;
        private Label _sizeLabel;
        private VisualElement _sizeContainer;
        
        private Toggle _mirrorToggle;
        private VisualElement _mirrorContent;
        private DropdownField _mirrorAxisDropdown;
        private DropdownField _mirrorBlendDropdown;
        
        private DropdownField _resolutionDropdown;
        private Toggle _useTextureFolderToggle;
        private VisualElement _savePathContainer;
        private TextField _savePathField;
        private Button _savePathBrowseButton;
        private Label _outsideAssetsWarning;
        private DropdownField _bgColorDropdown;
        private SliderInt _edgePaddingSlider;
        private Label _edgePaddingHelpLabel;
        private DropdownField _ditherModeDropdown;
        private Slider _ditherIntensitySlider;
        
        private DropdownField _blendModeDropdown;
        private Label _ndmfPreviewSuspendedLabel;
        
        private Button _bakeButton;
        private Label _statusLabel;
        
        private IVisualElementScheduledItem _statusResetSchedule;

        private DennokoVersionChecker.Result _versionResult =
            new DennokoVersionChecker.Result { State = DennokoVersionChecker.State.Checking, LocalVersion = "1.0.0" };

        public enum StatusType { Info, Success, Error }

        [MenuItem("dennokoworks/Gradation Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<GradationBakerWindow>();
            window.titleContent = new GUIContent("Gradation Baker");
            window.minSize = new Vector2(350, 500);
        }

        private void OnEnable()
        {
            if (_settings == null)    _settings    = new GradationSettings();
            if (_baker == null)       _baker       = new GradationBakingExecutor();
            if (_sceneHandle == null) _sceneHandle = new GradationSceneHandle();
            if (_preview == null)     _preview     = new GradationPreview();

            SceneView.duringSceneGui += OnSceneGUI;
            LocalizationManager.Initialize();
        }

        private void OnDisable()
        {
            // OnDisable はドメインリロード (スクリプト再コンパイル) や Play モード遷移でも呼ばれる。
            // プレビュー用プロキシは HideAndDontSave なのでここで破棄してよいが、
            // 作業メッシュは通常のシーンオブジェクトなので消してはいけない (OnDestroy で処理する)。
            SceneView.duringSceneGui -= OnSceneGUI;
            _sceneHandle?.Cleanup();
            _preview?.Cleanup();
            NdmfPreviewBridge.RestorePreview();
        }

        private void OnDestroy()
        {
            // ウィンドウを閉じたときだけ作業メッシュを片付ける
            CleanupAllWorkMeshes();
        }

        public void CreateGUI()
        {
            _rootElement = rootVisualElement;

            // 背景色や flex-grow は .dennoko-root として USS 側で定義される
            _rootElement.AddToClassList("dennoko-root");

            // OS のメイリオをウィンドウ全体のフォントに設定(全テキスト要素に継承される)。
            // 生成・保護・キャッシュ消失からの復帰はすべて DennokoUIFont が受け持つ。
            DennokoUIFont.Apply(_rootElement);

            // Load USS
            string ussPath = AssetDatabase.GUIDToAssetPath(USS_GUID);
            var uss = string.IsNullOrEmpty(ussPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (uss != null)
            {
                _rootElement.styleSheets.Add(uss);
            }
            else
            {
                Debug.LogWarning($"[GradationBakerWindow] USS not found. GUID: {USS_GUID}");
            }

            // Load UXML
            string uxmlPath = AssetDatabase.GUIDToAssetPath(UXML_GUID);
            var uxml = string.IsNullOrEmpty(uxmlPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (uxml == null)
            {
                _rootElement.Add(new Label("UXML Asset not found. Please check GUID."));
                return;
            }
            uxml.CloneTree(_rootElement);

            InitializeUIElements(_rootElement);
            BindUIEvents();
            UpdateLocalization();
            UpdateUIStates();
            StartVersionCheck();
        }

        private void InitializeUIElements(VisualElement root)
        {
            _versionLabel = root.Q<Label>("version-label");
            _versionReloadButton = root.Q<Button>("version-reload-button");
            _langButton = root.Q<Button>("lang-button");
            _enableToolToggle = root.Q<Toggle>("enable-tool-toggle");
            
            _toolDisabledCard = root.Q<VisualElement>("tool-disabled-card");
            _mainContentContainer = root.Q<VisualElement>("main-content-container");
            _meshListContainer = root.Q<VisualElement>("mesh-list-container");
            _createWorkMeshButton = root.Q<Button>("create-work-mesh-button");
            _clearAllButton = root.Q<Button>("clear-all-button");
            
            _gradientField = root.Q<GradientField>("gradient-field");
            _saveGradientButton = root.Q<Button>("save-gradient-button");
            _loadGradientButton = root.Q<Button>("load-gradient-button");
            
            _shapeDropdown = root.Q<DropdownField>("shape-dropdown");
            _centerField = root.Q<Vector3Field>("center-field");
            _centerResetButton = root.Q<Button>("center-reset-button");
            _rotationField = root.Q<Vector3Field>("rotation-field");
            _rotationResetButton = root.Q<Button>("rotation-reset-button");
            _heightField = root.Q<FloatField>("height-field");
            _sizeField = root.Q<Vector3Field>("size-field");
            _centerLabel = root.Q<Label>("center-label");
            _rotationLabel = root.Q<Label>("rotation-label");
            _sizeLabel = root.Q<Label>("size-label");
            _sizeContainer = root.Q<VisualElement>("size-container");
            
            _mirrorToggle = root.Q<Toggle>("mirror-toggle");
            _mirrorContent = root.Q<VisualElement>("mirror-content");
            _mirrorAxisDropdown = root.Q<DropdownField>("mirror-axis-dropdown");
            _mirrorBlendDropdown = root.Q<DropdownField>("mirror-blend-dropdown");
            
            _resolutionDropdown = root.Q<DropdownField>("resolution-dropdown");
            _useTextureFolderToggle = root.Q<Toggle>("use-texture-folder-toggle");
            _savePathContainer = root.Q<VisualElement>("save-path-container");
            _savePathField = root.Q<TextField>("save-path-field");
            _savePathBrowseButton = root.Q<Button>("save-path-browse-button");
            _outsideAssetsWarning = root.Q<Label>("outside-assets-warning");
            _bgColorDropdown = root.Q<DropdownField>("bg-color-dropdown");
            _edgePaddingSlider = root.Q<SliderInt>("edge-padding-slider");
            _edgePaddingHelpLabel = root.Q<Label>("edge-padding-help-label");
            _ditherModeDropdown = root.Q<DropdownField>("dither-mode-dropdown");
            _ditherIntensitySlider = root.Q<Slider>("dither-intensity-slider");
            
            _blendModeDropdown = root.Q<DropdownField>("blend-mode-dropdown");
            _ndmfPreviewSuspendedLabel = root.Q<Label>("ndmf-preview-suspended-label");
            
            _bakeButton = root.Q<Button>("bake-button");
            _statusLabel = root.Q<Label>("status-label");

            // Setup Dropdown Choices
            _shapeDropdown.choices = new List<string> { L("shape_linear"), L("shape_spherical") };
            _mirrorAxisDropdown.choices = new List<string> { "X", "Y", "Z" };
            _mirrorBlendDropdown.choices = new List<string> { L("mirror_blend_max"), L("mirror_blend_min") };
            _resolutionDropdown.choices = new List<string> { "128", "256", "512", "1024", "2048", "4096" };
            _bgColorDropdown.choices = new List<string> { L("bg_transparent"), L("bg_white"), L("bg_black") };
            _ditherModeDropdown.choices = new List<string> { L("dither_none"), "Interleaved Gradient Noise (IGN)", "Triangular Noise (TPDF)" };
            _blendModeDropdown.choices = new List<string> { L("blend_replace"), L("blend_additive"), L("blend_screen"), L("blend_multiply") };

            // Setup Drag and Drop
            SetupDragAndDrop(root.Q<VisualElement>("drop-area"));
        }

        private void BindUIEvents()
        {
            // Tool Toggle
            _enableToolToggle.value = _settings.IsToolActive;
            _enableToolToggle.RegisterValueChangedCallback(evt => {
                _settings.IsToolActive = evt.newValue;
                UpdateUIStates();
                SceneView.RepaintAll();
            });

            // Language Switch
            _langButton.clicked += () => {
                var nextLang = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese
                    ? LocalizationManager.Language.English
                    : LocalizationManager.Language.Japanese;
                LocalizationManager.SetLanguage(nextLang);
                
                // Re-initialize choices with new locale
                _shapeDropdown.choices = new List<string> { L("shape_linear"), L("shape_spherical") };
                _mirrorBlendDropdown.choices = new List<string> { L("mirror_blend_max"), L("mirror_blend_min") };
                _bgColorDropdown.choices = new List<string> { L("bg_transparent"), L("bg_white"), L("bg_black") };
                _ditherModeDropdown.choices = new List<string> { L("dither_none"), "Interleaved Gradient Noise (IGN)", "Triangular Noise (TPDF)" };
                _blendModeDropdown.choices = new List<string> { L("blend_replace"), L("blend_additive"), L("blend_screen"), L("blend_multiply") };

                UpdateLocalization();
                UpdateUIStates();
            };

            // Version Reload
            if (_versionReloadButton != null)
            {
                _versionReloadButton.clicked += () => {
                    GradationBakerVersion.ForceRecheck();
                    LoadVersionResultFromSessionState();
                };
            }

            // Buttons
            _createWorkMeshButton.clicked += () => {
                ToggleAllWorkMeshes();
                UpdateUIStates();
            };
            _clearAllButton.clicked += () => {
                ClearAllMeshes();
                UpdateUIStates();
            };
            _saveGradientButton.clicked += SaveGradientAsTexture;
            _loadGradientButton.clicked += () => {
                LoadGradientFromTexture();
                _gradientField.value = _settings.Gradient;
            };

            // Gradient Field
            _gradientField.value = _settings.Gradient;
            _gradientField.RegisterValueChangedCallback(evt => {
                _settings.Gradient = evt.newValue;
                SceneView.RepaintAll();
            });

            // Box Control
            _shapeDropdown.index = (int)_settings.Shape;
            _shapeDropdown.RegisterValueChangedCallback(evt => {
                _settings.Shape = (GradationShape)_shapeDropdown.index;
                UpdateUIStates();
                SceneView.RepaintAll();
            });

            _centerField.value = _settings.BoxCenter;
            _centerField.RegisterValueChangedCallback(evt => {
                _settings.BoxCenter = evt.newValue;
                SceneView.RepaintAll();
            });
            _centerResetButton.clicked += () => {
                // FitToAllMeshBounds は Center だけでなく Height/Size/Rotation も更新するので
                // 全フィールドをまとめて反映する
                _settings.FitToAllMeshBounds();
                UpdateUIStates();
                SceneView.RepaintAll();
            };

            _rotationField.value = _settings.BoxRotation.eulerAngles;
            _rotationField.RegisterValueChangedCallback(evt => {
                _settings.BoxRotation = Quaternion.Euler(evt.newValue);
                SceneView.RepaintAll();
            });
            _rotationResetButton.clicked += () => {
                _settings.BoxRotation = Quaternion.identity;
                _rotationField.SetValueWithoutNotify(Vector3.zero);
                SceneView.RepaintAll();
            };

            _heightField.value = _settings.BoxHeight;
            _heightField.RegisterValueChangedCallback(evt => {
                _settings.BoxHeight = Mathf.Max(0.001f, evt.newValue);
                SceneView.RepaintAll();
            });

            _sizeField.value = _settings.BoxScale;
            _sizeField.RegisterValueChangedCallback(evt => {
                _settings.BoxWidth = Mathf.Max(0.001f, evt.newValue.x);
                _settings.BoxHeight = Mathf.Max(0.001f, evt.newValue.y);
                _settings.BoxDepth = Mathf.Max(0.001f, evt.newValue.z);
                SceneView.RepaintAll();
            });

            // Mirror
            _mirrorToggle.value = _settings.UseMirror;
            _mirrorToggle.RegisterValueChangedCallback(evt => {
                _settings.UseMirror = evt.newValue;
                UpdateUIStates();
                SceneView.RepaintAll();
            });

            if (_settings.MirrorAxis == MirrorAxis.None)
                _settings.MirrorAxis = MirrorAxis.X;
            _mirrorAxisDropdown.index = (int)_settings.MirrorAxis - 1;
            _mirrorAxisDropdown.RegisterValueChangedCallback(evt => {
                _settings.MirrorAxis = (MirrorAxis)(_mirrorAxisDropdown.index + 1);
                SceneView.RepaintAll();
            });

            _mirrorBlendDropdown.index = (int)_settings.MirrorBlend;
            _mirrorBlendDropdown.RegisterValueChangedCallback(evt => {
                _settings.MirrorBlend = (MirrorBlendMode)_mirrorBlendDropdown.index;
                SceneView.RepaintAll();
            });

            // Output
            int[] resValues = { 128, 256, 512, 1024, 2048, 4096 };
            int currentResIndex = System.Array.IndexOf(resValues, _settings.Resolution);
            _resolutionDropdown.index = currentResIndex >= 0 ? currentResIndex : 4;
            _resolutionDropdown.RegisterValueChangedCallback(evt => {
                _settings.Resolution = resValues[_resolutionDropdown.index];
            });

            _useTextureFolderToggle.value = _settings.UseTextureFolder;
            _useTextureFolderToggle.RegisterValueChangedCallback(evt => {
                _settings.UseTextureFolder = evt.newValue;
                UpdateUIStates();
            });

            _savePathField.value = _settings.SavePath;
            _savePathField.RegisterValueChangedCallback(evt => {
                _settings.SavePath = evt.newValue;
                UpdateUIStates();
            });
            _savePathBrowseButton.clicked += () => {
                string path = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _settings.SavePath = "Assets" + path.Substring(Application.dataPath.Length);
                    else
                        _settings.SavePath = path;
                    _savePathField.value = _settings.SavePath;
                }
            };

            _bgColorDropdown.index = (int)_settings.BgColor;
            _bgColorDropdown.RegisterValueChangedCallback(evt => {
                _settings.BgColor = (BackgroundColor)_bgColorDropdown.index;
            });

            _edgePaddingSlider.value = _settings.EdgePaddingPixels;
            _edgePaddingSlider.RegisterValueChangedCallback(evt => {
                _settings.EdgePaddingPixels = evt.newValue;
                UpdateUIStates();
            });

            _ditherModeDropdown.index = (int)_settings.DitherMode;
            _ditherModeDropdown.RegisterValueChangedCallback(evt => {
                _settings.DitherMode = (DitherAlgorithm)_ditherModeDropdown.index;
                UpdateUIStates();
                SceneView.RepaintAll();
            });

            _ditherIntensitySlider.value = _settings.DitherIntensity;
            _ditherIntensitySlider.RegisterValueChangedCallback(evt => {
                _settings.DitherIntensity = evt.newValue;
                SceneView.RepaintAll();
            });

            // Preview
            _blendModeDropdown.index = (int)_settings.BlendMode;
            _blendModeDropdown.RegisterValueChangedCallback(evt => {
                _settings.BlendMode = (PreviewBlendMode)_blendModeDropdown.index;
                SceneView.RepaintAll();
            });

            // Bake Button
            _bakeButton.clicked += BakeAndSave;
        }

        private void UpdateLocalization()
        {
            _langButton.text = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese ? "EN" : "JA";
            _enableToolToggle.text = L("enable_tool");
            _toolDisabledCard.Q<Label>("tool-disabled-label").text = L("tool_disabled");

            _rootElement.Q<Label>("section-target-meshes-label").text = L("section_target_meshes");
            _rootElement.Q<Label>("mesh-help-label").text = L("mesh_help");
            _rootElement.Q<Label>("drop-meshes-here-label").text = L("drop_meshes_here");
            _clearAllButton.text = L("clear_all");

            _rootElement.Q<Label>("section-gradient-label").text = L("section_gradient");
            _gradientField.label = L("colors");
            _saveGradientButton.text = L("save_gradient");
            _loadGradientButton.text = L("load_gradient");

            _rootElement.Q<Label>("section-box-control-label").text = L("section_box_control");
            _rootElement.Q<Label>("box-help-label").text = L("box_help");
            _shapeDropdown.label = L("shape");
            
            _centerLabel.text = L("center");
            _rotationLabel.text = L("rotation");
            _centerField.label = "";
            _rotationField.label = "";
            
            _heightField.label = L("height");
            
            _sizeLabel.text = L("size");
            _sizeField.label = "";

            _mirrorToggle.text = L("mirror");
            _mirrorAxisDropdown.label = L("mirror_axis");
            _mirrorBlendDropdown.label = L("mirror_blend");
            _rootElement.Q<Label>("mirror-help-label").text = L("mirror_help");

            _rootElement.Q<Label>("section-output-label").text = L("section_output");
            _resolutionDropdown.label = L("resolution");
            _useTextureFolderToggle.text = L("use_texture_folder");
            _savePathField.label = L("save_path");
            _bgColorDropdown.label = L("bg_color");
            _edgePaddingSlider.label = L("edge_padding");
            _edgePaddingHelpLabel.text = L("edge_padding_help");
            _ditherModeDropdown.label = L("dither_mode");
            _ditherIntensitySlider.label = L("dither_intensity");

            _rootElement.Q<Label>("section-preview-label").text = L("section_preview");
            _blendModeDropdown.label = L("blend_mode");
            _ndmfPreviewSuspendedLabel.text = L("ndmf_preview_suspended");

            _bakeButton.text = L("bake_and_save");

            if (_versionReloadButton != null)
                _versionReloadButton.tooltip = L("recheck_update");

            ApplyVersionLabel();
            UpdateMeshList();
        }

        private void UpdateUIStates()
        {
            if (!_settings.IsToolActive)
            {
                _toolDisabledCard.style.display = DisplayStyle.Flex;
                _mainContentContainer.style.display = DisplayStyle.None;
                _bakeButton.SetEnabled(false);
                return;
            }

            _toolDisabledCard.style.display = DisplayStyle.None;
            _mainContentContainer.style.display = DisplayStyle.Flex;
            _bakeButton.SetEnabled(_settings.MeshEntries.Count > 0 && _settings.GetPrimaryRenderer() != null);

            // Work mesh button state
            bool hasAnyWorkMesh = HasAnyWorkMesh();
            _createWorkMeshButton.text = hasAnyWorkMesh ? L("delete_work_mesh") : L("create_work_mesh");

            // Box control shapes
            if (_settings.Shape == GradationShape.Linear)
            {
                _heightField.style.display = DisplayStyle.Flex;
                _sizeContainer.style.display = DisplayStyle.None;
            }
            else
            {
                _heightField.style.display = DisplayStyle.None;
                _sizeContainer.style.display = DisplayStyle.Flex;
            }

            // Mirror Section Content Display
            _mirrorContent.style.display = _settings.UseMirror ? DisplayStyle.Flex : DisplayStyle.None;

            // Output Folder and Path
            _savePathContainer.style.display = _settings.UseTextureFolder ? DisplayStyle.None : DisplayStyle.Flex;

            // Outside assets warning
            bool outsideAssets = !string.IsNullOrEmpty(_settings.SavePath) &&
                                 !_settings.SavePath.Replace('\\', '/').StartsWith("Assets");
            _outsideAssetsWarning.style.display = (outsideAssets && !_settings.UseTextureFolder) ? DisplayStyle.Flex : DisplayStyle.None;
            _outsideAssetsWarning.text = L("save_path_outside_assets");

            // Edge padding
            // パディングは透明ピクセルを膨張させる処理なので、背景が不透明だと何も起きない
            bool edgePaddingUsable = _settings.BgColor == BackgroundColor.Transparent;
            _edgePaddingSlider.SetEnabled(edgePaddingUsable);
            _edgePaddingHelpLabel.style.display =
                (edgePaddingUsable && _settings.EdgePaddingPixels > 0) ? DisplayStyle.Flex : DisplayStyle.None;

            // Dither settings
            _ditherIntensitySlider.style.display = _settings.DitherMode != DitherAlgorithm.None ? DisplayStyle.Flex : DisplayStyle.None;

            // NDMF Status
            _ndmfPreviewSuspendedLabel.style.display = NdmfPreviewBridge.IsSuppressing ? DisplayStyle.Flex : DisplayStyle.None;

            // Dynamic fields updates
            // SetValueWithoutNotify を使わないと ChangeEvent が発火し、登録済みコールバックが
            // 値を設定へ書き戻してしまう。特に回転は Quaternion → eulerAngles → Quaternion の
            // 往復で情報が失われ、SceneView のハンドル操作が引っかかる。
            _centerField.SetValueWithoutNotify(_settings.BoxCenter);
            _rotationField.SetValueWithoutNotify(_settings.BoxRotation.eulerAngles);
            _heightField.SetValueWithoutNotify(_settings.BoxHeight);
            _sizeField.SetValueWithoutNotify(_settings.BoxScale);
        }

        private void SetupDragAndDrop(VisualElement dropArea)
        {
            if (dropArea == null) return;

            dropArea.RegisterCallback<DragEnterEvent>(evt => {
                if (IsValidDragObject())
                {
                    dropArea.AddToClassList("dennoko-drop-area--hover");
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }
            });

            dropArea.RegisterCallback<DragLeaveEvent>(evt => {
                dropArea.RemoveFromClassList("dennoko-drop-area--hover");
            });

            dropArea.RegisterCallback<DragUpdatedEvent>(evt => {
                if (IsValidDragObject())
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }
            });

            dropArea.RegisterCallback<DragPerformEvent>(evt => {
                dropArea.RemoveFromClassList("dennoko-drop-area--hover");
                if (IsValidDragObject())
                {
                    DragAndDrop.AcceptDrag();
                    bool isFirstMesh = _settings.MeshEntries.Count == 0;

                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        Renderer renderer = null;

                        if (obj is GameObject go)
                            renderer = go.GetComponent<Renderer>();
                        else if (obj is Renderer r)
                            renderer = r;

                        if (renderer != null)
                        {
                            GameObject original = NdmfPreviewBridge.ResolveOriginal(renderer.gameObject);
                            if (original != renderer.gameObject)
                                renderer = original.GetComponent<Renderer>();
                        }

                        if (renderer != null)
                        {
                            bool alreadyExists = false;
                            foreach (var entry in _settings.MeshEntries)
                            {
                                if (entry.SourceRenderer == renderer)
                                {
                                    alreadyExists = true;
                                    break;
                                }
                            }

                            if (!alreadyExists)
                                _settings.MeshEntries.Add(new MeshEntry { SourceRenderer = renderer });
                        }
                    }

                    if (isFirstMesh && _settings.MeshEntries.Count > 0)
                        InitializeBoxFromRenderer(_settings.MeshEntries[0].SourceRenderer);

                    UpdateMeshList();
                    UpdateUIStates();
                    SceneView.RepaintAll();
                }
            });
        }

        private bool IsValidDragObject()
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go && go.GetComponent<Renderer>() != null)
                    return true;
                if (obj is Renderer)
                    return true;
            }
            return false;
        }

        private void UpdateMeshList()
        {
            if (_meshListContainer == null) return;
            _meshListContainer.Clear();

            for (int i = 0; i < _settings.MeshEntries.Count; i++)
            {
                var entry = _settings.MeshEntries[i];
                var element = CreateMeshEntryElement(entry, i);
                _meshListContainer.Add(element);
            }
        }

        private VisualElement CreateMeshEntryElement(MeshEntry entry, int index)
        {
            var item = new VisualElement();
            item.AddToClassList("dennoko-mesh-item");
            if (entry.HasWorkMesh)
            {
                item.AddToClassList("dennoko-mesh-item--active");
            }

            // Header line
            var header = new VisualElement();
            header.AddToClassList("dennoko-mesh-item-header");

            var foldout = new Foldout();
            foldout.text = "";
            foldout.value = entry.ShowDetails;
            foldout.RegisterValueChangedCallback(evt => {
                entry.ShowDetails = evt.newValue;
                UpdateMeshList();
            });
            header.Add(foldout);

            var objectField = new ObjectField();
            objectField.objectType = typeof(Renderer);
            objectField.value = entry.SourceRenderer;
            objectField.allowSceneObjects = true;
            objectField.AddToClassList("dennoko-grow");
            objectField.RegisterValueChangedCallback(evt => {
                var renderer = evt.newValue as Renderer;
                entry.SourceRenderer = renderer;
                if (renderer != null)
                {
                    if (_settings.MeshEntries.Count == 1 && index == 0)
                    {
                        InitializeBoxFromRenderer(renderer);
                    }
                    entry.UVChannel = _settings.UVChannel;
                    entry.MaskTexture = _settings.MaskTexture;
                    entry.UseVertexColorMask = _settings.UseVertexColorMask;
                    entry.InvertMask = _settings.InvertMask;
                    entry.SyncMaterialSlots(renderer);
                }
                UpdateMeshList();
                UpdateUIStates();
                SceneView.RepaintAll();
            });
            header.Add(objectField);

            if (entry.HasWorkMesh)
            {
                var statusLabel = new Label("[" + L("work") + "]");
                statusLabel.AddToClassList("dennoko-text-tertiary");
                statusLabel.AddToClassList("dennoko-mesh-tag");
                header.Add(statusLabel);
            }

            var removeBtn = new Button(() => {
                RemoveMeshEntry(index);
                UpdateUIStates();
            });
            removeBtn.text = "×";
            removeBtn.AddToClassList("dennoko-button-mini");
            header.Add(removeBtn);

            item.Add(header);

            // Detailed Settings
            if (entry.ShowDetails)
            {
                var details = new VisualElement();
                details.AddToClassList("dennoko-indent");
                details.AddToClassList("dennoko-mesh-item-details");

                // UV Channel
                var uvRow = new VisualElement();
                uvRow.AddToClassList("dennoko-horizontal");
                uvRow.AddToClassList("dennoko-field-row");
                var uvLabel = new Label(L("uv_channel"));
                uvLabel.AddToClassList("dennoko-field-label");
                uvRow.Add(uvLabel);
                var uvDropdown = new DropdownField();
                uvDropdown.choices = new List<string> { "UV0", "UV1", "UV2", "UV3" };
                uvDropdown.index = entry.UVChannel;
                uvDropdown.AddToClassList("dennoko-grow");
                uvDropdown.RegisterValueChangedCallback(evt => {
                    entry.UVChannel = uvDropdown.index;
                    SceneView.RepaintAll();
                });
                uvRow.Add(uvDropdown);
                details.Add(uvRow);

                // Mask Texture
                var maskRow = new VisualElement();
                maskRow.AddToClassList("dennoko-horizontal");
                maskRow.AddToClassList("dennoko-field-row");
                var maskLabel = new Label(L("mask_texture"));
                maskLabel.AddToClassList("dennoko-field-label");
                maskRow.Add(maskLabel);
                var maskField = new ObjectField();
                maskField.objectType = typeof(Texture2D);
                maskField.value = entry.MaskTexture;
                maskField.allowSceneObjects = false;
                maskField.AddToClassList("dennoko-grow");
                maskField.RegisterValueChangedCallback(evt => {
                    entry.MaskTexture = maskField.value as Texture2D;
                    SceneView.RepaintAll();
                });
                maskRow.Add(maskField);
                details.Add(maskRow);

                // Mask Options
                var optRow = new VisualElement();
                optRow.AddToClassList("dennoko-horizontal");
                optRow.AddToClassList("dennoko-field-row");
                optRow.AddToClassList("dennoko-field-offset");
                var useVertexToggle = new Toggle(L("use_vertex_color"));
                useVertexToggle.value = entry.UseVertexColorMask;
                useVertexToggle.RegisterValueChangedCallback(evt => {
                    entry.UseVertexColorMask = evt.newValue;
                    SceneView.RepaintAll();
                });
                optRow.Add(useVertexToggle);

                var invertMaskToggle = new Toggle(L("invert_mask"));
                invertMaskToggle.value = entry.InvertMask;
                invertMaskToggle.RegisterValueChangedCallback(evt => {
                    entry.InvertMask = evt.newValue;
                    SceneView.RepaintAll();
                });
                optRow.Add(invertMaskToggle);
                details.Add(optRow);

                // Split by Material
                var splitRow = new VisualElement();
                splitRow.AddToClassList("dennoko-horizontal");
                splitRow.AddToClassList("dennoko-field-row");
                splitRow.AddToClassList("dennoko-field-offset");
                var splitToggle = new Toggle(L("split_by_material"));
                splitToggle.value = entry.SplitByMaterial;
                splitToggle.RegisterValueChangedCallback(evt => {
                    entry.SplitByMaterial = evt.newValue;
                    UpdateMeshList();
                    SceneView.RepaintAll();
                });
                splitRow.Add(splitToggle);
                details.Add(splitRow);

                // Material slots
                Renderer activeRenderer = entry.ActiveRenderer;
                if (activeRenderer != null)
                {
                    entry.SyncMaterialSlots(activeRenderer);
                    Material[] mats = activeRenderer.sharedMaterials;
                    if (mats.Length > 0)
                    {
                        var slotHeaderRow = new VisualElement();
                        slotHeaderRow.AddToClassList("dennoko-horizontal");
                        slotHeaderRow.AddToClassList("dennoko-field-row");
                        var slotLabel = new Label(L("material_slots"));
                        slotLabel.AddToClassList("dennoko-field-label");
                        slotHeaderRow.Add(slotLabel);
                        details.Add(slotHeaderRow);

                        for (int mi = 0; mi < mats.Length; mi++)
                        {
                            int slotIndex = mi;
                            // 同じマテリアルや同名マテリアルが複数スロットに入っていると
                            // 名前だけでは行を区別できないため、常にスロット番号を前置する。
                            string matName = (mats[mi] != null)
                                ? $"[{mi}] {mats[mi].name}"
                                : $"[{mi}] {L("none")}";
                            var toggleRow = new VisualElement();
                            toggleRow.AddToClassList("dennoko-horizontal");
                            toggleRow.AddToClassList("dennoko-field-row");
                            toggleRow.AddToClassList("dennoko-field-offset");

                            var slotToggle = new Toggle(matName);
                            slotToggle.value = entry.EnabledMaterialSlots[slotIndex];
                            slotToggle.RegisterValueChangedCallback(evt => {
                                entry.EnabledMaterialSlots[slotIndex] = evt.newValue;
                                SceneView.RepaintAll();
                            });
                            toggleRow.Add(slotToggle);
                            details.Add(toggleRow);
                        }
                    }
                }

                item.Add(details);
            }

            return item;
        }

        // ─── Version checking ─────────────────────────────────────────────

        private void StartVersionCheck()
        {
            LoadVersionResultFromSessionState();
            GradationBakerVersion.StartCheckBackgroundTask();
        }

        internal void LoadVersionResultFromSessionState()
        {
            string local  = GradationBakerVersion.Current;
            string latest = SessionState.GetString(GradationBakerVersion.VerCheckLatestKey, string.Empty);
            bool   done   = SessionState.GetBool(GradationBakerVersion.VerCheckDoneKey, false);
            bool   error  = SessionState.GetBool(GradationBakerVersion.VerCheckErrorKey, false);

            DennokoVersionChecker.State state;
            if (!done)
                state = DennokoVersionChecker.State.Checking;
            else if (error || string.IsNullOrEmpty(latest))
                state = DennokoVersionChecker.State.Error;
            else if (DennokoVersionChecker.IsUpdateAvailable(latest, local))
                state = DennokoVersionChecker.State.UpdateAvailable;
            else
                state = DennokoVersionChecker.State.UpToDate;

            _versionResult = new DennokoVersionChecker.Result
            {
                State = state,
                LocalVersion = local,
                LatestVersion = latest,
                Url = SessionState.GetString(GradationBakerVersion.VerCheckUrlKey, string.Empty),
                Message = SessionState.GetString(GradationBakerVersion.VerCheckMessageKey, string.Empty)
            };
            ApplyVersionLabel();
        }

        private void ApplyVersionLabel()
        {
            if (_versionLabel == null) return;

            var r = _versionResult;
            string baseText = "v" + r.LocalVersion;
            string text;
            bool update = false, error = false;
            switch (r.State)
            {
                case DennokoVersionChecker.State.UpdateAvailable:
                    text = baseText + "  " + L("update_available", r.LatestVersion);
                    update = true;
                    break;
                case DennokoVersionChecker.State.Error:
                    text = baseText + "  " + L("cannot_check_version");
                    error = true;
                    break;
                case DennokoVersionChecker.State.Checking:
                    text = baseText + "  " + L("checking_version");
                    break;
                default:
                    text = baseText;
                    break;
            }
            _versionLabel.text = text;
            _versionLabel.EnableInClassList("dennoko-version-label--update", update);
            _versionLabel.EnableInClassList("dennoko-version-label--error", error);
        }

        // ─── Status bar ──────────────────────────────────────────────────

        public void SetStatus(string message, StatusType type, long autoResetMs = 3000)
        {
            if (_statusLabel == null) return;

            _statusLabel.text = message;
            _statusLabel.EnableInClassList("dennoko-status--success", type == StatusType.Success);
            _statusLabel.EnableInClassList("dennoko-status--error",   type == StatusType.Error);

            _statusResetSchedule?.Pause();
            if (type != StatusType.Info)
            {
                _statusResetSchedule = _statusLabel.schedule
                    .Execute(() => SetStatus("Ready", StatusType.Info))
                    .StartingIn(autoResetMs);
            }
        }

        // ─── Scene GUI ───────────────────────────────────────────────────────

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_settings == null || _preview == null || _sceneHandle == null) return;

            if (!_settings.IsToolActive || _settings.MeshEntries.Count == 0)
            {
                _preview.ClearProxies();
                NdmfPreviewBridge.RestorePreview();
                return;
            }

            NdmfPreviewBridge.SuppressPreview();
            _preview.UpdatePreviewAll(_settings);

            HandleChangeType changeType = _sceneHandle.DrawHandle(_settings, this);

            if (_settings.UseMirror && _settings.MirrorAxis != MirrorAxis.None)
                _sceneHandle.DrawMirrorHandle(_settings);

            if (changeType != HandleChangeType.None)
            {
                UpdateUIStates();
            }
        }

        // ─── Bake ────────────────────────────────────────────────────────────

        private void BakeAndSave()
        {
            FileLogger.Clear();
            FileLogger.Log("[GradationBakerWindow] Bake & Save clicked.");

            List<BakeResult> results = null;
            int savedCount = 0;
            List<string> savedPaths = new List<string>();

            try
            {
                results = _baker.BakeAll(_settings, (current, total, name) =>
                    EditorUtility.DisplayProgressBar(
                        "Gradation Baker",
                        L("progress_baking", name, current, total),
                        (current - 1) / (float)total));

                SaveBakeResults(results, savedPaths, ref savedCount);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                // 保存済みテクスチャは SaveTexture が破棄する。
                // 途中で例外が出た場合に残ったものをここで確実に解放する。
                DestroyRemainingTextures(results);
            }

            AssetDatabase.Refresh();
            ApplyTextureImportSettings(savedPaths);

            if (savedCount > 0)
            {
                string message = L("status_bake_success", savedCount);
                SetStatus(message, StatusType.Success);

                if (savedPaths.Count > 0 && TryGetAssetPath(savedPaths[0], out string firstAssetPath))
                {
                    Object textureAsset = AssetDatabase.LoadAssetAtPath<Object>(firstAssetPath);
                    if (textureAsset != null)
                    {
                        EditorGUIUtility.PingObject(textureAsset);
                        Selection.activeObject = textureAsset;
                    }
                }
            }
            else
            {
                SetStatus(L("status_bake_error"), StatusType.Error, 0);
            }
        }

        /// <summary>
        /// 保存した PNG のインポート設定を適用する。
        /// maxTextureSize を明示しないと、TextureImporter の既定値 (2048) によって
        /// 4096 でベイクしたテクスチャがインポート時に縮小されてしまう。
        /// </summary>
        private void ApplyTextureImportSettings(List<string> savedPaths)
        {
            if (savedPaths.Count == 0) return;

            // ファイル毎に SaveAndReimport すると同期リインポートが都度走るためまとめる
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string fullPath in savedPaths)
                {
                    if (!TryGetAssetPath(fullPath, out string assetPath)) continue;

                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null) continue;

                    importer.maxTextureSize = _settings.Resolution;
                    importer.alphaIsTransparency = _settings.BgColor == BackgroundColor.Transparent;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static void DestroyRemainingTextures(List<BakeResult> results)
        {
            if (results == null) return;

            foreach (var result in results)
            {
                if (result == null) continue;

                if (result.Texture != null) Object.DestroyImmediate(result.Texture);

                if (result.SubMeshResults == null) continue;
                foreach (var subRes in result.SubMeshResults)
                {
                    if (subRes.Texture != null) Object.DestroyImmediate(subRes.Texture);
                }
            }
        }

        private void SaveBakeResults(List<BakeResult> results, List<string> savedPaths, ref int savedCount)
        {
            foreach (var result in results)
            {
                if (result == null) continue;
                if (result.Texture == null && (result.SubMeshResults == null || result.SubMeshResults.Count == 0)) continue;

                string outputFolder = OutputPathResolver.ResolveOutputFolder(
                    result.SourceRenderer,
                    _settings.SavePath,
                    _settings.UseTextureFolder
                );

                OutputPathResolver.EnsureDirectoryExists(outputFolder);

                string fullDirPath = OutputPathResolver.ToFullPath(outputFolder);

                if (result.SubMeshResults != null && result.SubMeshResults.Count > 0)
                {
                    // 別々のマテリアルが同じ名前を持つ場合、ファイル名が衝突して
                    // 連番 (" 1") に落ちるため、どちらがどのスロットの出力か判別できなくなる。
                    // 衝突するときに限りスロット番号を付けて一意にする。
                    // (衝突しない一般ケースの名前は従来どおり変えない)
                    var nameCounts = new Dictionary<string, int>();
                    foreach (var subRes in result.SubMeshResults)
                    {
                        if (subRes.Texture == null) continue;

                        string name = SanitizeFileName(subRes.MaterialName);
                        nameCounts.TryGetValue(name, out int count);
                        nameCounts[name] = count + 1;
                    }

                    foreach (var subRes in result.SubMeshResults)
                    {
                        if (subRes.Texture == null) continue;

                        string safeMatName = SanitizeFileName(subRes.MaterialName);
                        if (nameCounts[safeMatName] > 1)
                        {
                            safeMatName = $"{safeMatName}_{subRes.SubMeshIndex}";
                        }

                        string baseName = $"{SanitizeFileName(result.RendererName)}_{safeMatName}";

                        string fileName = OutputPathResolver.GenerateUniqueFilename(fullDirPath, baseName);
                        string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');

                        SaveTexture(subRes.Texture, fullPath);

                        FileLogger.Log($"[GradationBakerWindow] Saved: {fullPath}");
                        savedPaths.Add(fullPath);
                        savedCount++;
                    }
                }
                else if (result.Texture != null)
                {
                    string fileName = OutputPathResolver.GenerateUniqueFilename(fullDirPath, SanitizeFileName(result.RendererName));
                    string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');

                    SaveTexture(result.Texture, fullPath);

                    FileLogger.Log($"[GradationBakerWindow] Saved: {fullPath}");
                    savedPaths.Add(fullPath);
                    savedCount++;
                }
            }
        }

        // ─── Mesh Helpers ────────────────────────────────────────────────────

        private void InitializeBoxFromRenderer(Renderer renderer)
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh != null)
            {
                MeshReadWriteEnabler.EnsureReadWriteEnabled(mesh);
                _settings.FitToAllMeshBounds();
                _settings.Resolution = TextureResolutionResolver.ResolveDefaultResolution(renderer);
                SceneView.RepaintAll();
            }
        }

        private bool HasAnyWorkMesh()
        {
            foreach (var entry in _settings.MeshEntries)
            {
                if (entry.HasWorkMesh) return true;
            }
            return false;
        }

        private void ToggleAllWorkMeshes()
        {
            bool hasAny = HasAnyWorkMesh();

            foreach (var entry in _settings.MeshEntries)
            {
                if (hasAny)
                {
                    if (entry.HasWorkMesh)
                    {
                        WorkMeshManager.DeleteWorkMesh(entry.WorkMeshObject);
                        entry.WorkMeshObject = null;
                    }
                }
                else
                {
                    if (!entry.HasWorkMesh && entry.SourceRenderer != null)
                        entry.WorkMeshObject = WorkMeshManager.CreateWorkMesh(entry.SourceRenderer);
                }
            }

            _settings.FitToAllMeshBounds();
            SceneView.RepaintAll();
        }

        private void RemoveMeshEntry(int index)
        {
            if (index < 0 || index >= _settings.MeshEntries.Count) return;

            var entry = _settings.MeshEntries[index];
            if (entry.HasWorkMesh)
                WorkMeshManager.DeleteWorkMesh(entry.WorkMeshObject);

            _settings.MeshEntries.RemoveAt(index);
            UpdateMeshList();
            SceneView.RepaintAll();
        }

        private void ClearAllMeshes()
        {
            CleanupAllWorkMeshes();
            _settings.MeshEntries.Clear();
            UpdateMeshList();
            SceneView.RepaintAll();
        }

        private void CleanupAllWorkMeshes()
        {
            if (_settings == null) return;
            foreach (var entry in _settings.MeshEntries)
            {
                if (entry.HasWorkMesh)
                {
                    WorkMeshManager.DeleteWorkMesh(entry.WorkMeshObject);
                    entry.WorkMeshObject = null;
                }
            }
        }

        private Mesh GetMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer smr) return smr.sharedMesh;
            if (renderer is MeshRenderer mr)
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                return mf ? mf.sharedMesh : null;
            }
            return null;
        }

        // ─── Gradient Save / Load ────────────────────────────────────────────

        private void SaveGradientAsTexture()
        {
            string gradientDir = "Assets/GeneratedGradation/gradation";
            if (!AssetDatabase.IsValidFolder(gradientDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/GeneratedGradation"))
                    AssetDatabase.CreateFolder("Assets", "GeneratedGradation");
                AssetDatabase.CreateFolder("Assets/GeneratedGradation", "gradation");
            }

            string fullDirPath = OutputPathResolver.ToFullPath(gradientDir);
            string fileName = GenerateUniqueGradientFilename(fullDirPath);
            string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');

            Texture2D tex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                tex.SetPixel(i, 0, _settings.Gradient.Evaluate(t));
            }
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(fullPath, bytes);
            AssetDatabase.Refresh();

            string assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            Object savedAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (savedAsset != null)
            {
                EditorGUIUtility.PingObject(savedAsset);
                Selection.activeObject = savedAsset;
            }

            FileLogger.Log($"[GradationBakerWindow] Gradient saved to: {fullPath}");
            SetStatus(L("status_gradient_saved"), StatusType.Success);
        }

        private string GenerateUniqueGradientFilename(string folderPath)
        {
            string baseName = "gradient";
            string extension = ".png";

            string fullPath = Path.Combine(folderPath, baseName + extension).Replace('\\', '/');
            if (!File.Exists(fullPath))
                return baseName + extension;

            int counter = 1;
            while (true)
            {
                string numberedName = $"{baseName} {counter}{extension}";
                fullPath = Path.Combine(folderPath, numberedName).Replace('\\', '/');

                if (!File.Exists(fullPath))
                    return numberedName;

                counter++;
                if (counter > 9999)
                    return $"{baseName}_{System.DateTime.Now:yyyyMMddHHmmss}{extension}";
            }
        }

        private void LoadGradientFromTexture()
        {
            string gradientDir = Path.Combine(Application.dataPath, "GeneratedGradation/gradation").Replace('\\', '/');
            if (!Directory.Exists(gradientDir))
                gradientDir = Application.dataPath;

            string path = EditorUtility.OpenFilePanel("Load Gradient", gradientDir, "png");

            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool ownsTex = false;
            if (tex == null || !tex.isReadable)
            {
                string fullPath = tex != null ? OutputPathResolver.ToFullPath(path) : path;
                if (!File.Exists(fullPath)) return;
                byte[] bytes = File.ReadAllBytes(fullPath);
                tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                ownsTex = true;
            }

            if (tex == null || tex.width < 2)
            {
                if (ownsTex && tex != null) Object.DestroyImmediate(tex);
                return;
            }

            Gradient gradient = new Gradient();
            int numKeys = Mathf.Min(8, tex.width);
            GradientColorKey[] colorKeys = new GradientColorKey[numKeys];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[numKeys];

            for (int i = 0; i < numKeys; i++)
            {
                float t = i / (float)(numKeys - 1);
                int x = Mathf.RoundToInt(t * (tex.width - 1));
                Color c = tex.GetPixel(x, 0);

                colorKeys[i] = new GradientColorKey(c, t);
                alphaKeys[i] = new GradientAlphaKey(c.a, t);
            }

            gradient.SetKeys(colorKeys, alphaKeys);
            _settings.Gradient = gradient;

            if (ownsTex) Object.DestroyImmediate(tex);

            SceneView.RepaintAll();
            FileLogger.Log($"[GradationBakerWindow] Gradient loaded from: {path}");
        }

        // ─── Utilities ───────────────────────────────────────────────────────

        private static bool TryGetAssetPath(string fullPath, out string assetPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            string dataPath = Application.dataPath;
            if (normalized.StartsWith(dataPath))
            {
                assetPath = "Assets" + normalized.Substring(dataPath.Length);
                return true;
            }
            assetPath = null;
            return false;
        }

        private string L(string key) => LocalizationManager.Get(key);
        private string L(string key, params object[] args) => LocalizationManager.Get(key, args);

        /// <summary>
        /// GameObject 名やマテリアル名はファイル名に使えない文字を含みうるため置換する。
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        }

        private void SaveTexture(Texture2D tex, string path)
        {
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            Object.DestroyImmediate(tex);
        }
    }
}
