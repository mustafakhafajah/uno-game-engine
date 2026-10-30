using UnoEngine.Core.Cards;
using UnoEngine.Core.Definitions;
using UnoEngine.Core.Events;
using UnoEngine.Core.Randomness;
using UnoEngine.Engine.Mediator;

namespace UnoEngine.Engine.Managers;

/// <summary>Owns the draw pile and the discard pile: shuffling, drawing, discarding and reshuffling.</summary>
public sealed class DeckManager(IGameMediator mediator, IRandom random) : GameManager(mediator)
{
    private readonly List<Card> _drawPile = [];
    private readonly List<Card> _discardPile = [];
    private readonly Dictionary<string, string> _chosenAttributes = new(StringComparer.OrdinalIgnoreCase);

    public int DrawPileCount => _drawPile.Count;
    public int DiscardPileCount => _discardPile.Count;
    public Card? TopCard => _discardPile.Count == 0 ? null : _discardPile[^1];

    /// <summary>The top card's attributes, with any chosen value (e.g. a Wild's colour) on top.</summary>
    public IReadOnlyDictionary<string, string> TopAttributes
    {
        get
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (TopCard is { } top)
            {
                foreach (var (key, value) in top.Attributes)
                    attributes[key] = value;
            }

            foreach (var (key, value) in _chosenAttributes)
                attributes[key] = value;
            return attributes;
        }
    }

    /// <summary>Creates one card per copy, numbered from 1, and shuffles them into the draw pile.</summary>
    public IReadOnlyList<Card> CreateCards(GameDefinition definition)
    {
        var id = 0;
        return definition.Cards
            .SelectMany(d => Enumerable.Range(0, d.Copies).Select(_ => new Card(++id, d)))
            .ToList();
    }

    /// <summary>Puts every given card in the draw pile (piles emptied first) and shuffles.</summary>
    public void Reset(IEnumerable<Card> cards)
    {
        _drawPile.Clear();
        _discardPile.Clear();
        _chosenAttributes.Clear();
        _drawPile.AddRange(cards);
        _drawPile.Shuffle(random);
    }

    public IReadOnlyList<Card> CollectAllCards()
    {
        var cards = _drawPile.Concat(_discardPile).ToList();
        _drawPile.Clear();
        _discardPile.Clear();
        _chosenAttributes.Clear();
        return cards;
    }

    /// <summary>Draws up to <paramref name="count"/> cards, reshuffling the discard pile (minus its top card) when needed.</summary>
    public IReadOnlyList<Card> Draw(int count)
    {
        var cards = new List<Card>(count);
        while (cards.Count < count)
        {
            if (_drawPile.Count == 0 && !ReshuffleDiscardPile())
                break;
            cards.Add(_drawPile[0]);
            _drawPile.RemoveAt(0);
        }

        return cards;
    }

    public void Discard(Card card)
    {
        _discardPile.Add(card);
        _chosenAttributes.Clear();
    }

    /// <summary>Discards cards beneath the top card, so the card in play stays on top.</summary>
    public void DiscardUnderTop(IEnumerable<Card> cards) =>
        _discardPile.InsertRange(Math.Max(0, _discardPile.Count - 1), cards);

    public void ChooseAttribute(string attribute, string value) => _chosenAttributes[attribute] = value;

    /// <summary>Turns the top of the draw pile face up to start the discard pile.</summary>
    public Card FlipFirstCard()
    {
        var card = Draw(1).Single();
        Discard(card);
        return card;
    }

    /// <summary>Puts the top discard back into the draw pile and shuffles (for the "draw another" first-card rule).</summary>
    public void ReturnTopCardAndShuffle()
    {
        var top = TopCard ?? throw new InvalidOperationException("The discard pile is empty.");
        _discardPile.RemoveAt(_discardPile.Count - 1);
        _drawPile.Add(top);
        _drawPile.Shuffle(random);
    }

    private bool ReshuffleDiscardPile()
    {
        if (_discardPile.Count <= 1)
            return false;
        var top = _discardPile[^1];
        _drawPile.AddRange(_discardPile.Take(_discardPile.Count - 1));
        _discardPile.Clear();
        _discardPile.Add(top);
        _drawPile.Shuffle(random);
        Notify(new DeckReshuffled(_drawPile.Count));
        return true;
    }

    // Test support: set up exact situations without relying on the shuffle.
    internal Card TakeFromDrawPile(string definitionId)
    {
        var card = _drawPile.Find(c => c.Definition.Id == definitionId)
                   ?? throw new InvalidOperationException($"No '{definitionId}' left in the draw pile.");
        _drawPile.Remove(card);
        return card;
    }

    internal void ReturnToDrawPile(IEnumerable<Card> cards) => _drawPile.AddRange(cards);

    internal void PutOnTopOfDrawPile(Card card) => _drawPile.Insert(0, card);
}
