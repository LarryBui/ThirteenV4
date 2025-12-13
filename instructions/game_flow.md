here's the game flow and terminologies.

## game workflows
* a player starts the app, the home screen has 2 buttons, Play and Quit. 
* when a player clicks on Play, Nakama server will find if there is any match with less than 4 players. 
* If there is none, it will create a new match and make this player Owner. 
* it also assign seat 1 to this player. 
* when a player leaves and he's the match owner, the server will pick a random player and make him owner. 
* There is always 4 seats in a match. when there is a player join a match, the lower seat # will be assigned first

## game within Match
* when in a match, the owner can start a game
* server will create new deck and deals 13 random cards to each player in the match
* game ends when the everyone empty their hand cards exception the last one.
* when game ends, the owner can start a new game.

## rules within game of Tien Len (Thirteen)
* when a player play cards, he starts a round
* when the next player skips/passes, he cannot play again until this "round" is over
* a round is considered over when everyone else skips/passes. the board is clear
* the last player who played card can play again
* anyone who empties their cards first will be first winner.
* game will continue until there is only one left

## expecting events in app
* PlayerJoined
* PlayerLeft
* GameStarted
* CardPlayed
* TurnPassed
* GameEnded
* 
