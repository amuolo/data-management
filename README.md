# data-management

This is a general-purpose configurable template web-application to tackle Data Management at any level.

## Table Of Contents

  * [Introduction](#introduction)
  * [Theory](#theory)
      * [Agency](#agency)
      * [Message Hub](#message-hub)
      * [Job](#job)
  * [Getting started](#getting-started)

## Introduction

The aim of this repo is to provide an infrastructure that allows developers to quickly setup a modern and scalable 
web application based following the famous [Actor Model]("https://en.wikipedia.org/wiki/Actor_model").

The application can be configured both following the WPF or the Blazor patterns, and relies strongly on the 
[SignalR]("https://en.wikipedia.org/wiki/SignalR").

In its essence, this repo intends to promote modularity and scalability, helping developers to build parallel 
and distributed applications.

> The Actor Model organizes complex computations in autonomous entities called "actors" which can work concurrently. 
> Each actor manages its own state by executing CRUD (Create, Retrieve, Update, Delete) operations, and is allowed
> to exchange messages with any other actor asynchronously. 

> SignalR is a real-time communication library for web applications, enabling bi-directional communication 
> between servers and clients. It allows the exchange of messages and ensures a dynamic and responsibe interaction
> in real-time applications.

## Theory

### Agency

> The Agency project is the cornerstone of this repo. It provides an easy-to-use API to define agents, managers, and offices.

Aligned with the actor model, this framework empowers developers to swiftly configure numerous concurrent actors, referred to as *agents*. 
These agents are not deployed immediately; instead, deployment occurs only when strictly necessary, managed by dedicated *managers*. 
Additionally, the client front end can configure *offices*, which function as independent actors capable of communicating 
with other agents seamlessly over the wire.

> Agents are defined uniquely with *name*, *state*, and *contract*.

Agents can exchange messages only through their own contract interface. Additionally, agents are stateful entities, 
owning a specific state and processing CRUD operations when handling the messages they receive. 
Since messages are handled sequentially, there is no need for a locking mechanism when multiple resources (agents) attempt to access the same resource.

While Message hub is the project storing all structures and APIs to deal with message handling, Job is the dedicated state handler.

### Message Hub

While the Agency uses the structures defined in the Message Hub project, this can also be used as standalone.
It provides the *MessageHub* class to handle message posting with and without response, a background service *Post* to process messages in the 
outbox queue sequentially and safely, the *PostingHub* defining all the methods invokable on the SignalR server, and a series of static stateless 
methods that are generally usable to configure the SignalR hub connection with both default and dedicated functionalities.

Together, the class, the service, the server hub, and the configuration methods, give a generalized mechanism to deal with a wide range of scenarios. 
The developer can freely decide when to rely on the request-response pattern and when to simply broadcast streams of events (messages) to all the connected clients.
This flexibility was from the beginning enforced on the design of this library to broaden the range of applications and facilitate its adoption.

### Job

The project named "Job" provides an abstraction layer to manage a state undergoing several updates in a step-by-step approach. 
Job is thread-safe and guarantees that each step is executed following the simple first-in-first-out (FIFO) rule.
Thanks to its clean and easy-to-use API, it helps the developer to quickly set up logging and visualize progress 
for the given operation to be started.

## Getting started

This is a typical configuration of the Program: 

     builder.Services.AddSignalR();

     var workplace = new Workplace("https://localhost:7158") with
     {
         AgentTypes = [typeof(Agent<Model, DataHub, IDataContract>)],
         HireAgentsPeriod = TimeSpan.FromMinutes(30),
         DecommissionerWaitingTime = TimeSpan.FromSeconds(10),
     };

     builder.Services.AddHostedService<Manager>()
                     .AddSingleton(workplace);

Here below is how to configure one office on the WPF client side:

     var office = Office<IApp>.Create(BaseUrl)
                              .Register(agent => agent.DataChangedEvent, DataUpdate)
                              .Register(agent => agent.ShowProgress, ShowProgress)
                              .Register(agent => agent.Display, Logger)
                              .AddAgent<Model, DataHub, IDataContract>().Run();

here the configuration of a different office with logging capabilities on a different client

     var office = Office<IAgencyContract>.Create(NavManager.BaseUri)
                                         .ReceiveLogs((sender, senderId, message) =>
                                         {
                                             var formattedMessage = $"{sender}: {message}";
                                             messages.Add(formattedMessage);
                                             InvokeAsync(StateHasChanged);
                                         })
                                         .Run();

and here a draft of the IDataContract interface

     public interface IDataContract : IAgencyContract
     {
         /* in */
         Task<List<string>> ReadRequest();

         Task ImportRequest(string fileName);

         /* out */
         Task DataChangedEvent();

         Task ShowProgress(double progress);

         Task Display(string message);
     }

with the example of posting API from one client office

     Office.Post(agent => agent.ImportRequest, GetSelectedFile());

---


