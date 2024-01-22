# data-management

This is a general-purpose configurable template web-application to tackle Data Management at any level.

## Table Of Contents

  * [Getting started](#getting-started)
  * [Agency](#agency)
  * [Job](#job)

## Getting Started

The aim of this repo is to provide an infrastructure that allows developers to quickly setup a modern and scalable 
web application based following the famous <a href="https://en.wikipedia.org/wiki/Actor_model" target="_blank">Actor Model</a>.

The application can be configured both following the WPF or the Blazor patterns, and relies strongly on the 
<a href="https://en.wikipedia.org/wiki/SignalR" target="_blank">SignalR</a>.

In its essence, this repo intends to promote modularity and scalability, helping developers to build parallel 
and distributed applications.

<details><summary> <strong>Actor Model</strong></summary>

The Actor Model organizes complex computations in autonomous entities called "actors" which can work concurrently. 
Each actor manages its own state by executing CRUD (Create, Retrieve, Update, Delete) operations, and is allowed
to exchange messages with any other actor asynchronously. 

</details>

<details><summary> <strong>SignalR</strong></summary>

SignalR is a real-time communication library for web applications, enabling bi-directional communication 
between servers and clients. It allows the exchange of messages and ensures a dynamic and responsibe interaction
in real-time applications.

</details>

## Agency

The project named "Agency" is the cornerstone of this repo. It provides an easy-to-use API to define agents, managers, and offices.
In this framework, the agents are actors which are handled by their managers and organized in offices.
Agents are allowed to exchange only those messages that have been formalized in their own contract interfaces. 

## Job

The project named "Job" provides an abstraction layer to manage a state undergoing several steps. 
Job is thread-safe and guarantees that each step is executed following the simple rule first-in-first-out (FIFO).
Thanks to its clean and easy-to-use API, it helps the developer to quickly set up logging and visualize progress 
for the given operation to be started.

---


