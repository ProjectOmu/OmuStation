crewlet-name = {$name}let
crewlet-description = Aww, it's like a stupid little {$name}!

crewlet-examine-likes = {CAPITALIZE(SUBJECT($crewlet))} {CONJUGATE-BASIC($crewlet, "seem", "seems")} to like you.
crewlet-examine-loves = {CAPITALIZE(SUBJECT($crewlet))} {CONJUGATE-BASIC($crewlet, "love", "loves")} you.
crewlet-examine-dislikes = {CAPITALIZE(SUBJECT($crewlet))} {CONJUGATE-BASIC($crewlet, "seem", "seems")} to dislike you.
crewlet-examine-hates = {CAPITALIZE(SUBJECT($crewlet))} {CONJUGATE-BASIC($crewlet, "hate", "hates")} you.

crewlet-abandoned = {CAPITALIZE(THE($crewlet))} doesn't want to follow you anymore.
crewlet-adopted = {CAPITALIZE(THE($crewlet))} seems to have taken a liking to you.
crewlet-pickpocket = {CAPITALIZE(THE($crewlet))} is tugging at your {$item}!

crewlet-looking = It's looking at you.
crewlet-stare = …
crewlet-recall = {$name}... where are you...
crewlet-dread = You feel a sense of dread.

crewlet-merge-start = {CAPITALIZE(THE($crewlet))} reaches into the anomaly!
crewlet-merge = {CAPITALIZE(THE($crewlet))} sinks into the anomaly!
crewlet-merged-anomaly-examine = [color=#c8323c]It looks disturbingly similar to {$name}.[/color]
crewlet-disguise-name = {$name}
crewlet-husk-map-name = Crewlet husk storage

crewlet-omen-warning = You feel like you've lost part of yourself.
crewlet-omen-warning-chat = [italic]{$message}[/italic]

entity-effect-guidebook-crewlet-gestation =
    { $chance ->
        [1] Grows
        *[other] grow
    } a small version of the metabolizer once {$dose}u builds up. It follows whoever injected that dose.
