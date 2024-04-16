# data-management

This is a general-purpose configurable template web-application to tackle Data Management at any level.

## Table Of Contents

  * [Introduction](#intro)
  * [Theory](#theory)
  * [Agency](#agency)
  * [MessageHub](#message-hub)
  * [Job](#job)
  * [Getting started](#getting-started)

<a name="intro"></a>
## Introduction

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

<a name="theory"></a>
## Theory

<a name="agency"></a>
### Agency

The Agency project is the cornerstone of this repo. It provides an easy-to-use API to define agents, managers, and offices.

Aligned with the actor model, this framework empowers developers to swiftly configure numerous concurrent actors, referred to as <em>agents</em>. 
These agents are not deployed immediately; instead, deployment occurs only when strictly necessary, managed by dedicated <em>managers</em>. 
Additionally, the client front end can configure <em>offices</em>, which function as independent actors capable of communicating 
with other agents seamlessly over the wire.

> Agents are defined uniquely with <em>name</em>, <em>state</em>, and <em>contract</em>.

Agents can exchange messages only through their own contract interface. Additionally, agents are stateful entities, 
owning a specific state and processing CRUD operations based on the type of message they receive. 
Since messages are handled sequentially, there is no need for a locking mechanism when multiple resources (agents) attempt to access the same resource.

MessageHub...
Job...

<a name="message-hub"></a>
### Message Hub

About Message Hub project...

<a name="job"></a>
### Job

The project named "Job" provides an abstraction layer to manage a state undergoing several steps. 
Job is thread-safe and guarantees that each step is executed following the simple rule first-in-first-out (FIFO).
Thanks to its clean and easy-to-use API, it helps the developer to quickly set up logging and visualize progress 
for the given operation to be started.

<a name="getting-started"></a>
## Getting started

TODO

---


