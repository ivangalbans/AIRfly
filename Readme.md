AIRfly
======

Descripción
------------

AIRfly es una aplicación web para descargar, subir y escuchar música `online`, al estilo `Napster` y `Spotify`.

Arquitectura
-------------

La arquitectura utilizada fue `cliente-servidor` para atender a pedidos de los usuarios y `P2P` entre los servidores de la aplicación.

Descripción Técnica
--------------------

* Los servidores fueron implementados de manera distribuida. Se implementó una tabla hash distribuida conocida como `Chord` [1]. El lenguaje seleccionado para el desarrollo del proyecto fue `C#`, con la tecnología Windows Communication Foundation (WCF) la cual fue creada con el fin de permitir una programación rápida de sistemas distribuidos y el desarrollo de aplicaciones basadas en arquitecturas orientadas a servicios, con una API simple; y que puede ejecutarse en una máquina local, una LAN, o sobre Internet en una forma segura.

¿Por que se seleccionó Chord como tabla hash distribuida?
--------------------------------------------------------

* Chord es una implementacion de DHT sencilla y poderosa, práctica para ser desarrollada en los cursos de Sistemas Distribuidos impartidos en pregrado universitario.

Problemas encontrados
---------------------

1. Estabilización de anillo después de la caída de nodos: Cuando un nodo se une al anillo de Chord se le calcula una lista de nodos llamados SuccessorCache la cual se encarga de tener los n sucesores consecutivos al nodo actual (en nuestra implementacion n = 3). La solución propuesta fue crear un hilo que actualizara la SuccessorCache cada un tiempo establecido.

2. Incorporación de un nodo al anillo después de su caída: Cuando un nodo se levanta después de haber abandonado el anillo, es necesario que ocupe su posición correspondiente en este. Para esto utilizamos un hilo que se encarga cada cierto tiempo de descubrir nuevos nodos en la red y luego unirlos a dicha red.

3. Descubrir nuevos nodos en la red: WCF cuenta con una librería llamada `Discovery` la cual contiene clases y métodos que resuelven dicha problemática.

4. Robustez del sistema: Para garantizar la robuztes del sistema, cada vez que un nodo recibe la información que le corresponde, este la trasmite hacia su sucesor (idea utilizada en nuestra implementación, fácilmente modificable a nivel de código).

Como correr la aplicación
--------------------------

Server:

~~~bash
AIRFly\AIRFlyServer\bin\Release> Server.exe [numero de puerto] [Ruta de almacenamiento]
~~~

Web:

~~~bash
AIRFly\AIRflyWebApp>  dotnet run
~~~

Recomendaciones
---------------

* Mejorar el funcionamiento asíncrono de la aplicación en general.
* Implementar una base de datos para la aplicación web.
* Mejorar el sitio web de la aplicación.

Experimentación
----------------

El sistema fue probado con 4 computadoras reales, cada una con 4 servidores virtuales de Chord corriendo, y una de estas con los servicios de la web. Luego se conectaron 4 clientes (via móvil) y utilizaron las funcionalidades que brinda la aplicación a través de la web. La interrupción por parte de servidores fue probada y el sistema continuo de manera estable y sin pérdida de información, así como la llegada de un nuevo servidor a la red, el cual se estabilizó satisfactoriamente.

Requerimientos
---------------

* .NET Framework 4.7
* ASP.NET Core 2.1.2

Bibliografía
-------------

[1] Ion Stoica, Robert Morris, David Karger, M. Frans Kaashoek, Hari Balakrishnan. Chord: A Scalable Peertopeer Lookup Service for Internet Applications.

Puede contactarnos
-------------------

* 2235penton@gmail.com
* ivan.galban.smith@gmail.com
* raydelalonsobaryolo.gmail.com

Authors
-------

* Abel Penton Ibrahim
* Ivan Galban Smith
* Raydel E. Alonso Baryolo