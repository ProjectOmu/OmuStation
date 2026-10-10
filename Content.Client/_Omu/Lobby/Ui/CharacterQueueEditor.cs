// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Client._Omu.Preferences;
using Content.Client.Interaction;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared.Preferences;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Omu.Lobby.Ui;

public sealed class CharacterQueueEditor
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IClientPreferencesManager _preferences = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly BoxContainer _characters;
    private readonly Action _reload;
    private readonly CharacterQueueSystem _characterQueue;
    private readonly DragDropHelper<CharacterPickerButton> _dragHelper;
    private readonly List<(int Slot, CharacterPickerButton Button)> _pickers = new();
    private readonly PanelContainer _dropMarker = new()
    {
        MinSize = new Vector2(0, 2),
        StyleClasses = { StyleClass.Highlight },
    };

    private PanelContainer? _dragShadow;

    public CharacterQueueEditor(BoxContainer characters, Action reload)
    {
        IoCManager.InjectDependencies(this);

        _characters = characters;
        _reload = reload;
        _characterQueue = _entities.System<CharacterQueueSystem>();
        _dragHelper = new DragDropHelper<CharacterPickerButton>(OnBeginDrag, OnContinueDrag, OnEndDrag);
    }

    public void Attach()
    {
        _characterQueue.Updated += OnQueueUpdated;
    }

    public void Detach()
    {
        _characterQueue.Updated -= OnQueueUpdated;
        _dragHelper.EndDrag();
    }

    public void Update(float frameTime)
    {
        _dragHelper.Update(frameTime);
    }

    public IEnumerable<KeyValuePair<int, ICharacterProfile>> OrderedCharacters(PlayerPreferences prefs)
    {
        return _characterQueue.GetOrderedSlots(prefs, _characterQueue.State)
            .Select(slot => new KeyValuePair<int, ICharacterProfile>(slot, prefs.Characters[slot]));
    }

    public void Clear()
    {
        _pickers.Clear();
    }

    public void Add(int slot, CharacterPickerButton picker)
    {
        _pickers.Add((slot, picker));
        picker.SetActive(_characterQueue.State.ActiveSlots.Contains(slot));
        picker.OnActiveToggled += active => _characterQueue.SetActive(slot, active);

        picker.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick)
                _dragHelper.MouseDown(picker);
        };

        picker.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            var dropped = _dragHelper.IsDragging && _dragHelper.Dragged == picker;
            var index = GetDropIndex(picker);
            _dragHelper.EndDrag();

            if (dropped)
                _characterQueue.MoveCharacter(slot, index);
        };
    }

    private void OnQueueUpdated()
    {
        if (_preferences.Preferences is not { } prefs)
            return;

        var slots = _characterQueue.GetOrderedSlots(prefs, _characterQueue.State);
        if (!slots.SequenceEqual(_pickers.Select(p => p.Slot)))
        {
            _reload();
            return;
        }

        foreach (var (slot, picker) in _pickers)
        {
            picker.SetActive(_characterQueue.State.ActiveSlots.Contains(slot));
        }
    }

    private int GetDropIndex(CharacterPickerButton dragged)
    {
        var mouse = _ui.MousePositionScaled.Position.Y;
        return _pickers.Count(p => p.Button != dragged && p.Button.GlobalRect.Center.Y < mouse);
    }

    private bool OnBeginDrag()
    {
        if (_pickers.Count < 2 || _dragHelper.Dragged is not { } dragged)
            return false;

        var slot = _pickers.First(p => p.Button == dragged).Slot;
        _dragShadow = new PanelContainer
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
            StyleClasses = { StyleClass.PanelDark },
            Children =
            {
                new Label
                {
                    Text = _preferences.Preferences?.Characters[slot].Name,
                    Margin = new Thickness(6, 2),
                },
            },
        };

        _ui.PopupRoot.AddChild(_dragShadow);
        return true;
    }

    private bool OnContinueDrag(float frameTime)
    {
        if (_dragShadow == null || _dragHelper.Dragged == null)
            return false;

        var mouse = _ui.MousePositionScaled.Position;
        LayoutContainer.SetPosition(_dragShadow, mouse + new Vector2(12, 0));

        if (_dropMarker.Parent != _characters)
            _characters.AddChild(_dropMarker);

        var index = _pickers.Count(p => p.Button.GlobalRect.Center.Y < mouse.Y);
        _dropMarker.SetPositionInParent(index);
        return true;
    }

    private void OnEndDrag()
    {
        _dragShadow?.Orphan();
        _dragShadow = null;
        _dropMarker.Orphan();
    }
}
