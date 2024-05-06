# Muoland Agency - Data Management

[![NuGet](https://img.shields.io/nuget/v/muoland.agency.svg?style=flat-square)](https://www.nuget.org/packages/muoland.agency)
[![Nuget](https://img.shields.io/nuget/dt/muoland.agency)](https://www.nuget.org/packages/muoland.agency)

This library and NuGet package provides a robust and versatile solution designed to facilitate system architecture. 
Whether you’re building microservices, distributed applications, or event-driven systems, 
this API provides the tools you need to design, construct, and maintain high-performance, 
concurrent solutions using the actor concurrency model.

## Table Of Contents

  * [Introduction](#introduction)
  * [Theory](#theory)
      * [Agency](#agency)
      * [Message Hub](#message-hub)
      * [Job Factory](#job-factory)
  * [Getting started](#getting-started)

## Introduction

The aim is to provide an infrastructure that allows developers to quickly setup a modern and scalable 
web application based following the famous [Actor Model](https://en.wikipedia.org/wiki/Actor_model).

The application can be configured freely following your favourite tools and architecture, and depends almost solely 
on the [SignalR](https://en.wikipedia.org/wiki/SignalR) library.

In its essence, this repo intends to promote modularity and scalability, helping developers to build parallel 
and distributed applications.

> The Actor Model organizes complex computations in autonomous entities called "actors" which can work concurrently. 
> Each actor manages its own state by executing CRUD (Create, Retrieve, Update, Delete) operations, and is allowed
> to exchange messages with any other actor asynchronously. 

> SignalR is a real-time communication library for web applications, enabling bi-directional communication 
> between servers and clients. It allows the exchange of messages and ensures a dynamic and responsible interaction
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

### Job Factory

The project named "Job" provides an abstraction layer to manage a state undergoing several updates in a step-by-step approach. 
Job is thread-safe and guarantees that each step is executed following the simple first-in-first-out (FIFO) rule.
Thanks to its clean and easy-to-use API, it helps the developer to quickly set up logging and visualize progress 
for the given operation to be started.

## Getting started

This is a typical configuration of the Program: 

     using Enterprise.Agency;
     using Enterprise.MessageHub;

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

and here the configuration of a different office with logging capabilities on a different client

     var office = Office<IAgencyContract>.Create(NavManager.BaseUri)
                                         .ReceiveLogs((sender, senderId, message) =>
                                         {
                                             var formattedMessage = $"{sender}: {message}";
                                             messages.Add(formattedMessage);
                                             InvokeAsync(StateHasChanged);
                                         })
                                         .Run();

From the Program configuration we learn there is going to be one agent (actor) deployed when needed. 
This is a simple data agent owning some 'Model' and performing some operation, e.g. import, update, deletion.
Moreover, from the API we learn that the first office will be using this agent, that is, the client 
defining this office will eventually need to access the data Model. 

Within this framework, the team responsible of architecting the data agent would need to set up 
both the contract and the hub. While the former defines all possible incoming or outgoing requests
that this agent can handle, the latter provides the corresponding methods that will be called 
automatically when each given request is processed. 

An example of the IDataContract interface is the following:

     public interface IDataContract : IAgencyContract
     {
         /* in */
         Task<MyData> ReadRequest();

         Task ImportRequest(string fileName);

         /* out */
         Task DataChangedEvent();

         Task ShowProgress(double progress);

         Task Display(string message);
     }

whereas the corresponding DataHub reads

     public class DataHub : MessageHub<IDataContract>
     {
         public async Task<Model> CreateRequest(Model model)
         {
            await model.Database.ReadAllAsync();
            return model;
         }

         public async Task<List<string>> ReadRequest(Model model)
         {
            return model.GetPrintable();
         }

         public async Task ImportRequest(string fileName, Model model)
         {
             if (fileName is null || fileName is "")
             {
                Post(agent => agent.Display, $"Cannot process null or empty fileName");
                return;
             }

             var dirInfo = new DirectoryInfo(".");
             var files = dirInfo.GetFiles("*.*", SearchOption.AllDirectories);
             var file = files.FirstOrDefault(x => x.FullName.Contains(fileName));

             Post(agent => agent.ShowProgress, 1d/3);

             model.Update(DataOperators.Import(file));

             Post(agent => agent.Display, $"File {fileName} imported");
             Post(agent => agent.ShowProgress, 2d/3);

             model.Process();

             Post(agent => agent.Display, $"Data has been processed");
             Post(agent => agent.ShowProgress, 3d/3);
             Post(agent => agent.DataChangedEvent);
         }
     }

Note the usage of outgoing messages 'ShowProgress', 'Display', and 'DataChangedEvent'
to signal the state of the process and any eventual change on the data. 
Clients can subscribe to these messages providing the corresponding handler.

From the client side the kick off of an Import Request is achievable with the following 
simple API:

     office.Post(agent => agent.ImportRequest, fileName);

---


