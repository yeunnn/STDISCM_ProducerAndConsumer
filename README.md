# STDISCM_ProblemSet3_ProducerAndConsumer

A producer-consumer exercise involving file writes and network sockets. This will help you practice concurrent programming, file I/O, queueing, and network communication.  This is a simple simulation of a media upload service.

There are two main components

1) Producer: Reads the a video file, uploads it to the media upload service (consumer).
2) Consumer:  Accepts simultaneous media uploads.  Saves the uploaded videos to a single folder. Displays and previews the 10 seconds of the uploaded video file.

Both the producer and consumer runs on a diffrent virtual machine and communicate with each other via the network / sockets.

## Slides
https://docs.google.com/presentation/d/10XEbUDBgvXKmFlyqTkkJC8RKzks3FSEd/edit?usp=sharing&ouid=101928301120544085464&rtpof=true&sd=true

## Depenencies and Prerequisites
- Use Visual Studio 2022 w/ .NET desktop development and Desktop development with C++
- Use Oracle VirtualBox (latest version)
    - Setup a Windows 11 virtual machine
    - Setup the same Visual Studio 2022 setup

## Installing
- Clone the repository to your Visual Studio 2022 program (in both your machine and virtual machine)

## Executing program
- To simulate the programs, select the Program.c (in the virtual machine) to run the Consumer project and simulate it as the Consumer
- Select the Producer.cs (in your machine) to run the Producer project and simulate it as the Producer
- In the Consumer program, input the necessary fields such as consumer threads, queue capacity, and listening port
- In the Producer program, input the necesary fields such as Consumer IP and Consumer Port
    - To know Consumer IP, ensure the virtual machine has its network set to Bridged Adapter
    - Then in the virtual machine, go to cmd and enter ipconfig and get the IPv4 Address
- After inputting the IP and port, input the number of producer threads (# of directories) where the videos are located and then input the DIRECT PATH of each directory
- On success, the videos should be uploaded and reflected to the Consumer program for viewing and etc.