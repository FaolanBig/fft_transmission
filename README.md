# FFTT – Dateiübertragung über spektrale Mehrfrequenz-Codierung

## Übersicht

FFTT ist ein C#-Konsolenprogramm, das beliebige Binärdateien in eine Audiospur codiert und diese anschließend mithilfe einer Fast Fourier Transform (FFT) wieder decodieren kann. Die Datenübertragung erfolgt über ein fest definiertes Spektrum von 32 parallelen Frequenzkanälen, wobei jeder Kanal einem einzelnen Bit entspricht. Jedem Datenblock von vier Bytes (32 Bit) wird ein eigener Frequenzsatz zugeordnet, der anschließend als kurzer Tonabschnitt im WAV-Format ausgegeben wird.

Die Nutzung von Frequenzmultiplexing ermöglicht eine robuste und klar abgegrenzte spektrale Darstellung der Bitwerte und bildet die Grundlage für die spätere spektrale Analyse im Decodierungsschritt.

> :information_source: FFTT kann für alle Dateien verwendet werden, deren Inhalt unabhängig von Header und Metadaten funktionsfähig ist.

Das Projekt steht unter der GNU Affero General Public License v3.

---

## Technischer Hintergrund

### Spektrale Datenkodierung

Die Codierung beruht auf einem einfachen, aber präzisen Prinzip:

1. Ein Datenblock besteht aus 4 Bytes = 32 Bits.
2. Für jedes Bit existiert eine eindeutig definierte Frequenz  
   `F(bit) = BaseFreq + bitIndex * FreqStep`.
3. Ist ein Bit gesetzt (1), wird die zugehörige Frequenz als Sinusanteil dem Audiosignal hinzugefügt.
4. Alle aktiven Frequenzen werden überlagert und bilden so den „Tonblock“.
5. Es folgt eine kurze Stille, um Tonblöcke voneinander abzugrenzen.
6. Der resultierende Abschnitt wird normalisiert und als PCM-16-bit-Mono in eine WAV-Datei geschrieben.

Diese Form der Datenübertragung ist eine Form von Multitone-Modulation, bei der das Informationssignal nicht zeitlich, sondern spektral verteilt ist.

### Spektrale Datenrekonstruktion (FFT-Analyse)

Bei der Decodierung wird folgender Prozess durchgeführt:

1. Das WAV-Signal wird blockweise gelesen, wobei pro Block exakt die Länge eines Tonsegments berücksichtigt wird.
2. Jeder Block wird auf die nächste Zweierpotenz aufgefüllt und der FFT unterzogen.
3. Die FFT liefert für jede spektrale Komponente Amplitudenwerte.
4. Für jede der 32 erwarteten Frequenzen wird deren Amplitude aus dem Frequenzspektrum gelesen.
5. Liegt die Amplitude über einem definierten Schwellenwert, gilt das Bit als gesetzt.
6. Die 32 Bits werden zu einem 4-Byte-Block zusammengesetzt.
7. Alle Blöcke werden zu einer Binärdatei (`decoded.bin`) zusammengeführt.

Die Methode funktioniert zuverlässig, solange sich die Frequenzen ausreichend unterscheiden und das Eingangssignal nicht zu stark verrauscht oder verzerrt ist.

## Architektur und Parameter

Die wichtigsten Parameter befinden sich am Anfang des Programms:

- `SampleRate = 44100`  
  Abtastrate des Audiosignals in Hz.

- `ToneDuration = 0.1`  
  Dauer eines Tonabschnitts (100 ms).

- `SilenceDuration = 0.05`  
  Dauer der anschließenden Stille (50 ms).

- `BitsPerBlock = 32`  
  Anzahl der Bits pro Frequenzblock (entspricht 4 Bytes).

- `BaseFreq = 500.0`  
  Startfrequenz für das niedrigste Bit.

- `FreqStep = 75.0`  
  Frequenzabstand zwischen zwei Bitkanälen.

Diese Parameter sind frei modifizierbar. Ein größerer Frequenzabstand erhöht die Robustheit gegen Überlappungen, führt jedoch zu größerem Bandbreitenbedarf.

## Verwendung

### Codierung

Eine Datei wird folgendermaßen in eine WAV-Audiodatei umgewandelt:

```bash
fftt encode <input-file>
```

Beispiel:

```bash
fftt encode text.txt
```

Ausgabe:

* `encoded.wav`
  Enthält alle codierten Datenblöcke.

### Decodierung

Eine zuvor generierte WAV-Datei kann wieder in eine Binärdatei zurückgeführt werden:

```bash
fftt decode <audio-file>
```

Beispiel:

```bash
fftt decode encoded.wav
```

Ausgabe:

* `decoded.bin`
  Enthält die aus dem Spektrum rekonstruierten Daten.

> Jetzt kann die Binärdatei mit einem Texteditor (wie z.B. Visual Studio Code) geöffnet werden.
> Dateispezifische Parameter wie die Zeichenencodierung müssen eventuell manuell im Texteditor angepasst werden.

## Aufbau des WAV-Formats im Programm

Das Programm schreibt WAV-Dateien im PCM-Mono-Format:

* 16 Bit pro Sample
* 44.1 kHz Abtastrate
* RIFF-Header
* `fmt`-Chunk
* `data`-Chunk mit den codierten Samples

Die Implementierung des Headers erfolgt manuell mittels `BinaryWriter`.

Beim Einlesen wird der WAV-Header vollständig geparst, um Format, Sampleanzahl und Kanäle zu bestimmen. Anschließend werden alle Samples in ein `double[]`-Array überführt.

## Logging

Das Programm verwendet die [BigLog](https://biglog.bigvault.cloud)-Bibliothek für strukturierte Ausgaben. Eigenschaften:

* Terminal- und Datei-Logging
* Farbige Präfixe für Warnungen, Fehler und Status
* Ausgabe in `fftt.log`

Dies erleichtert das Debugging während der Entwicklung und Analyse.

## Lizenz

Dieses Projekt steht unter der GNU Affero General Public License Version 3 oder später.

Die vollständige Lizenz ist verfügbar unter:

[https://www.gnu.org/licenses/](https://www.gnu.org/licenses/)
