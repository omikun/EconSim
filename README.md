# ECONSIM

An agent-based economy simulator in Unity3D based on "[Emergent Economies for Role Playing Games](https://ianparberry.com/pubs/econ.pdf)," [bazzarBot](https://github.com/larsiusprime/bazaarBot), and [bazzarBot2](https://github.com/Vibr8gKiwi/bazaarBot2/).

![Screen Capture](screencapture_v01_480p.gif)

**Features:**
- ~~Agent-based price beliefs that governs price range to in bids.~~
- Prices are based on cost of inputs + profit margin
- Commodity dependencies - If food is dependent on wood and there is a forest fire, the supply of wood drops and the price of food sky rockets. Non-farmers go bankrupt as a result.
 - AuctionHouse - all sellers enter their asking price and bidders buy from lowest price up.
 - Agents that go bankrupt respawn in a more lucrative profession; corollary: *bankruptcy drives growth*.
 - Taxes - A government collects taxes on profits, uses money to fund respawns or stimulate economy.
 
**Roadmap:**
v0.2
 - User interactions
    - Submit bid/asks on the auction
    - Tax wealth/revenue/profit
    - Welfare cash
v0.3
 - death and births of agents - population grows and shrinks with economic conditions
v0.4
 - Banks - can make loans based on leverage ratio, create credit bubbles.
 - Agent development - agents invest surplus cash to develop new production abilities to become bigger, may develop scaling overheads.
 - Mergers - agents can buy competitions out.
 - Foreign markets - multiple instances of auction houses with its own set of agents and its own set of commodities.
 - International trades - agents can make trades in foreign markets; local markets may impose import tariffs (player's choice).
 - Separate currencies - each market has its own set of currencies; inflation rate; exchange rate.
 - Hegemony - win condition
   - gain or lose points based on how other nations react to you (fear, respect, dependence)

**Instructions:**
 - Download Unity3D Personal Edition
 - Download this repository
 - Open folder in Unity
 - Press Play button

Number of agents and init conditions can be found in the "auction house" game object properties in the Inspector panel. 

This code is released under the MIT license.
