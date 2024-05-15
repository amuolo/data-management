### 14 May 2024 Release Note v2.0.0 ###

v2 is here 💛💙💜💚💘

This package is for you! Now released with the more permissive MIT license!

This is a major version update, i.e. the public API is slightly different than v1.

List of changes to the public API: 

- Workplace replaced with Agency Culture
- Introduced fluent API to configure Agency Services and set options
- Office replaced with Project to exploit the triad: project-agent-manager
- Job Start(+Async) method to reflect asynchronism
- introduced IHubAddress and HubAddress to start working on address configuration

List of other changes: 

- now we support more complex topology with more Managers for scalability
- Agents Discovery mechanism greatly refactored to support multiple Managers
- agency culture members safety increased via encapsulation
- relaxed constraint: agent state must not be default constructible anymore
- code cleanups and simplifications

### 6 May 2024 Release Note v1.1.0 🚀🚀🚀 ###

- new API: register to job on finish events
- simplify hub contract
- better test coverage
- posting hub consistency improved
- agents state simplified
- improve job execution mechanism

### 30 Apr 2024 Release Note v1.0.7 ###

- fix bugs in job state machine and step handling
- performance optimization with parallel job executions

### 26 Apr 2024 Release Note v1.0.6 🚀 ### 

- fix bug hindering message recognition from predicates
- fix inner agent state
- Introduce disposability for message hubs
- Greatly improve test coverage

### 23 Apr 2024 - v1.0.5 ###

- Key improvements in the Manager services
- Added office registrations with responses
- Disposal mechanism refactored 

🚀 🚀 🚀

### 21 Apr 2024 - v1.0.4 ###

Better aesthetics and documentation. 🌟

### 21 Apr 2024 - v1.0.3 ###

Several bug fixes and general performance improvements in the job factory engine. 🚀

### 19 Apr 2024 - v1.0.2 ###

Bug fix: Asynchronous message handling has been rectified!
This necessitated a revamp of the manager recruitment process
to sequentially process incoming messages, while guaranteeing
the safe and automatic initiation of agents. 🚀

### 17 Apr 2024 - v1.0.1 ###

Fix documentation and visualization on nuget.org

### 17 Apr 2024 - v1.0.0 ###

Introducing our new Concurrency Model Package! 🚀

Purpose: This package simplifies concurrent programming by providing robust abstractions for managing parallel execution.
Key Features:
Agents: Easily configure and manage concurrent actors (agents) with their own contract interfaces.
Stateful Entities: Agents maintain state and process CRUD operations based on incoming messages.
Sequential Message Handling: No need for locking mechanisms; messages are processed sequentially.

Upgrade your concurrency game with our powerful package! Get started today. 🌟
