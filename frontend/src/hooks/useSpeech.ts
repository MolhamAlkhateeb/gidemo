import { useCallback, useRef, useState } from "react";
import { api } from "../api";

/**
 * Voice input via the browser Web Speech API (SpeechRecognition) and voice output
 * via Amazon Polly (server) with a browser speechSynthesis fallback.
 */
export function useSpeech() {
  const [listening, setListening] = useState(false);
  const [speaking, setSpeaking] = useState(false);
  const recognitionRef = useRef<any>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const startListening = useCallback((onResult: (text: string) => void) => {
    const SR =
      (window as any).SpeechRecognition ||
      (window as any).webkitSpeechRecognition;
    if (!SR) {
      alert("Speech recognition is not supported in this browser.");
      return;
    }
    const recognition = new SR();
    recognition.lang = "en-US";
    recognition.interimResults = false;
    recognition.continuous = false;
    recognition.onresult = (e: any) => {
      const transcript = Array.from(e.results)
        .map((r: any) => r[0].transcript)
        .join(" ");
      onResult(transcript);
    };
    recognition.onend = () => setListening(false);
    recognition.onerror = () => setListening(false);
    recognition.start();
    recognitionRef.current = recognition;
    setListening(true);
  }, []);

  const stopListening = useCallback(() => {
    recognitionRef.current?.stop();
    setListening(false);
  }, []);

  const speak = useCallback(async (text: string, voiceId?: string) => {
    try {
      setSpeaking(true);
      const blob = await api.textToSpeech(text, voiceId);
      const url = URL.createObjectURL(blob);
      const audio = new Audio(url);
      audioRef.current = audio;
      audio.onended = () => {
        setSpeaking(false);
        URL.revokeObjectURL(url);
      };
      await audio.play();
    } catch {
      // Fallback to browser TTS.
      const utter = new SpeechSynthesisUtterance(text);
      utter.onend = () => setSpeaking(false);
      window.speechSynthesis.speak(utter);
    }
  }, []);

  const stopSpeaking = useCallback(() => {
    audioRef.current?.pause();
    window.speechSynthesis.cancel();
    setSpeaking(false);
  }, []);

  return { listening, speaking, startListening, stopListening, speak, stopSpeaking };
}
