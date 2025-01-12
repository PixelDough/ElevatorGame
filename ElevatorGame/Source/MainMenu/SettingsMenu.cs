using Engine;
using FmodForFoxes.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ElevatorGame.Source.MainMenu;

public class SettingsMenu
{
    public SettingsTabs CurrentTab => _currentTab;

    public Action OnClose { get; set; }

    public static int DividerX { get; private set; } = 64;

    public enum SettingsTabs : int
    {
        Audio = 0,
        Interface = 1,
        Graphics = 2
    }

    private SettingsTabs _currentTab = SettingsTabs.Audio;

    private SettingsTab Tab => _tabs[(int)CurrentTab];

    private RenderTarget2D _renderTarget;

    private float _opacity;
    private int _opacityTarget = 1;

    private bool _closed;

    private List<SettingsTab> _tabs = [];

    public void LoadContent()
    {
        _renderTarget = new(MainGame.Graphics.GraphicsDevice, MainGame.GameBounds.Width, MainGame.GameBounds.Height);

        _tabs = [
            new SettingsTab {
                TitleLangToken = GetTabLangToken("audio"),
                Options = [
                    new SettingsOptionSlider(index: 0, width: 96, minValue: 0, maxValue: 100, stepAmount: 10) {
                        GetValue = ()
                            => MathUtil.FloorToInt(StudioSystem.GetParameterTargetValue("VolumeMaster") * 100),
                        SetValue = (value)
                            => StudioSystem.SetParameterValue("VolumeMaster", value / 100f),
                        LangToken = GetOptionLangToken("audio", "master_volume"),
                        SetSelected = GetOptionSelectedAction(tab: 0),
                    },
                    new SettingsOptionSlider(index: 1, width: 96, minValue: 0, maxValue: 100, stepAmount: 10) {
                        GetValue = ()
                            => MathUtil.FloorToInt(StudioSystem.GetParameterTargetValue("VolumeMusic") * 100),
                        SetValue = (value)
                            => StudioSystem.SetParameterValue("VolumeMusic", value / 100f),
                        LangToken = GetOptionLangToken("audio", "music_volume"),
                        SetSelected = GetOptionSelectedAction(tab: 0),
                    },
                    new SettingsOptionSlider(index: 2, width: 96, minValue: 0, maxValue: 100, stepAmount: 10) {
                        GetValue = ()
                            => MathUtil.FloorToInt(StudioSystem.GetParameterTargetValue("VolumeSounds") * 100),
                        SetValue = (value)
                            => StudioSystem.SetParameterValue("VolumeSounds", value / 100f),
                        LangToken = GetOptionLangToken("audio", "sfx_volume"),
                        SetSelected = GetOptionSelectedAction(tab: 0),
                    },
                ],
            },
            new SettingsTab {
                TitleLangToken = GetTabLangToken("interface"),
                Options = [],
            },
            new SettingsTab {
                TitleLangToken = GetTabLangToken("graphics"),
                Options = [],
            }
        ];

        foreach (var t in _tabs)
        {
            foreach (var o in t.Options)
            {
                o.LoadContent();
            }
        }
    }

    public void Update()
    {
        if (Keybindings.GoBack.Pressed && !_closed)
        {
            _opacityTarget = 0;
            OnClose?.Invoke();
            _closed = true;
        }

        _opacity = MathUtil.ExpDecay(_opacity, _opacityTarget, 10, 1f / 60f);

        if (_closed) return;

        int tabCycleDir = (Keybindings.SettingsTabNext.Pressed ? 1 : 0) - (Keybindings.SettingsTabLast.Pressed ? 1 : 0);
        if (tabCycleDir != 0)
        {
            int tab = (int)_currentTab;

            tab = (tab + tabCycleDir) % _tabs.Count;
            if (tab < 0) tab = _tabs.Count - 1;

            SetTab((SettingsTabs)tab);
        }
        else if (Tab.Options.Count != 0)
        {
            int inputDir = (Keybindings.Down.Pressed ? 1 : 0) - (Keybindings.Up.Pressed ? 1 : 0);
            Tab.SetSelectedOption((Tab.SelectedOption + inputDir) % Tab.Options.Count);
            if (Tab.SelectedOption < 0) Tab.SetSelectedOption(Tab.Options.Count - 1);
        }

        foreach (var o in Tab.Options)
        {
            o.Update(Tab.SelectedOption == o.Index);
        }
    }

    public void PreDraw(SpriteBatch spriteBatch)
    {
        MainGame.Graphics.GraphicsDevice.SetRenderTarget(_renderTarget);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        {
            MainGame.Graphics.GraphicsDevice.Clear(Color.Black * 0.5f);

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool isActiveTab = (int)_currentTab == i;
                Vector2 tabTitlePos = new(
                    2 + (isActiveTab ? 2 : 0),
                    2 + i * 10
                );

                // spriteBatch.DrawStringSpacesFix(
                //     MainGame.FontBold,
                //     LocalizationManager.Get(_tabs[i].TitleLangToken),
                //     tabTitlePos + Vector2.One,
                //     Color.Black,
                //     6
                // );
                spriteBatch.DrawStringSpacesFix(
                    MainGame.FontBold,
                    LocalizationManager.Get(_tabs[i].TitleLangToken),
                    tabTitlePos,
                    isActiveTab ? Color.Yellow : Color.White,
                    6
                );
            }

            foreach (var o in Tab.Options)
            {
                o.Draw(spriteBatch, Tab.SelectedOption == o.Index);
            }
        }
        spriteBatch.End();
        MainGame.Graphics.GraphicsDevice.Reset();
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(_renderTarget, Vector2.Zero, Color.White * _opacity);
    }

    public void SetTab(SettingsTabs tab)
    {
        Tab.SetSelectedOption(0);
        _currentTab = tab;
    }

    private static string GetTabLangToken(string name)
    {
        const string prefix = "ui.settings.tab";
        return $"{prefix}.{name}";
    }

    private static string GetOptionLangToken(string tab, string name)
    {
        const string prefix = "ui.settings.tab";
        const string infix = "option";
        return $"{prefix}.{tab}.{infix}.{name}";
    }

    private Action<int> GetOptionSelectedAction(int tab)
    {
        return i => _tabs[tab].SetSelectedOption(i);
    }

    class SettingsTab
    {
        private int _selectedOption;

        public int SelectedOption => _selectedOption;

        public required List<ISettingsOption> Options { get; set; }

        public required string TitleLangToken { get; set; }

        public void SetSelectedOption(int optionIndex)
        {
            _selectedOption = optionIndex;
        }
    }
}