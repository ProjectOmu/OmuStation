# SPDX-License-Identifier: AGPL-3.0-or-later
# copied from Resources/Prototypes/_Omu/Entities/Objects/Misc/yinjisol_cards.yml

card-examined = This is the {$target}.
cards-verb-shuffle = Shuffle
card-verb-shuffle-success = Cards shuffled
cards-verb-draw = Draw card
cards-verb-flip = Flip cards
card-verb-join = Join cards
card-verb-organize-success = Cards flipped face { $facedown ->
    [true]   down
    *[false] up
}
cards-verb-organize-up = Flip cards face up
cards-verb-organize-down = Flip cards face down
cards-verb-pickcard = Pick a card
card-stack-examine = { $count ->
    [one] There is {$count} card in this stack.
    *[other] There are {$count} cards in this stack.
}
cards-stackquantitychange-added = Card was added (Total cards: {$quantity})
cards-stackquantitychange-removed = Card was removed (Total cards: {$quantity})
cards-stackquantitychange-joined = Stack was merged (Total cards: {$quantity})
cards-stackquantitychange-split = Stack was split (Total cards: {$quantity})
cards-stackquantitychange-unknown = Stack count changed (Total cards: {$quantity})
cards-verb-convert-to-deck = Convert to deck
cards-verb-split = Split in half

card-base-name = card
card-deck-name = deck of cards

red_0 = Red 0
red_1 = Red 1
red_2 = Red 2
red_3 = Red 3
red_4 = Red 4
red_5 = Red 5
red_6 = Red 6
red_7 = Red 7
red_8 = Red 8
red_9 = Red 9
red_lock = Red Lock
red_plus2 = Red +2
red_swap = Red Swap

blue_0 = Blue 0
blue_1 = Blue 1
blue_2 = Blue 2
blue_3 = Blue 3
blue_4 = Blue 4
blue_5 = Blue 5
blue_6 = blue 6
blue_7 = blue 7
blue_8 = blue 8
blue_9 = blue 9
blue_lock = blue Lock
blue_plus2 = blue +2
blue_swap = blue Swap

green_0 = green 0
green_1 = green 1
green_2 = green 2
green_3 = green 3
green_4 = green 4
green_5 = green 5
green_6 = green 6
green_7 = green 7
green_8 = green 8
green_9 = green 9
green_lock = green Lock
green_plus2 = green +2
green_swap = green Swap

yellow_0 = yellow 0
yellow_1 = yellow 1
yellow_2 = yellow 2
yellow_3 = yellow 3
yellow_4 = yellow 4
yellow_5 = yellow 5
yellow_6 = yellow 6
yellow_7 = yellow 7
yellow_8 = yellow 8
yellow_9 = yellow 9
yellow_lock = yellow Lock
yellow_plus2 = yellow +2
yellow_swap = yellow Swap

rainbow = Rainbow wildcard
rainbow_draw = Rainbow +4

container-sealed = A still-sealed pack of Solarian Yinji cards. Yinji: the legally distinct color- and number-matching game. First to one card wins!
container-unsealed = The seal attached to it dissipates.

