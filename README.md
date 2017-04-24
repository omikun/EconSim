# ECONSIM

An agent-based economy simulator in Unity3D based on "[Emergent Economies for Role Playing Games](http://larc.unt.edu/techreports/LARC-2010-03.pdf)" and [bazzarBot](https://github.com/larsiusprime/bazaarBot).

![Screen Capture](ScreenCapture.gif)

**Features:**
- Agent-based price beliefs that governs price range to in bids.
- Price beliefs are adjusted based on the success of each bid and the price trends of the commodity.
- Commodity dependencies - If food is dependent on wood and there is a forest fire, the supply of wood drops and the price of food sky rockets. Non-farmers go bankrupt as a result.
 - Double-blind auction - all sellers enter their asking price and all buyers enter their asking price blindly for the current round but has access to historical data.
 - Agents that go bankrupt respawn in a more lucrative profession; corollary: *bankruptcy drives growth*.
 
**Roadmap:**
 - Taxes - A government collects taxes on all agents, uses money to help bankruptcy or stimulate economy, can also make loans.
 - Banks - can make loans based on leverage ratio, create credit bubbles.
 - Agent development - agents invest surplus cash to develop new production abilities to become bigger, may develop scaling overheads.
 - Mergers - agents can buy competitions out.
 - Foreign markets - multiple instances of auction houses with its own set of agents and its own set of commodities.
 - International trades - agents can make trades in foreign markets; local markets may impose import tariffs (player's choice).
 - Separate currencies - each market has its own set of currencies; inflation rate; exchange rate.

**Instructions:**
 - Download Unity3D Personal Edition
 - Download this repository
 - Open folder in Unity
 - Press Play button

Number of agents and init conditions can be found in the "auction house" game object properties in the Inspector panel. Commodity dependencies and info are in the Commodity.cs file

This code is released under the MIT license.
