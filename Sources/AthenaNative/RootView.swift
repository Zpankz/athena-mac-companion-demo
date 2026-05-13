import AthenaNativeCore
import SwiftUI

struct RootView: View {
    @EnvironmentObject private var model: AthenaAppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            HStack(alignment: .center, spacing: 14) {
                ZStack {
                    Circle()
                        .fill(.linearGradient(colors: [.teal, .indigo], startPoint: .topLeading, endPoint: .bottomTrailing))
                    Text("A")
                        .font(.system(size: 42, weight: .semibold, design: .rounded))
                        .foregroundStyle(.white)
                }
                .frame(width: 92, height: 92)

                VStack(alignment: .leading, spacing: 6) {
                    Text("Athena")
                        .font(.system(size: 32, weight: .semibold))
                    Text(model.statusText)
                        .font(.title3)
                        .foregroundStyle(.secondary)
                }
            }

            HStack(spacing: 12) {
                Button {
                    model.toggleVoice()
                } label: {
                    Label(model.isVoiceRunning ? "Stop Voice" : "Start Voice", systemImage: model.isVoiceRunning ? "mic.slash.fill" : "mic.fill")
                }
                .keyboardShortcut(.space, modifiers: [.command])
                .buttonStyle(.borderedProminent)

                Button {
                    model.saveSettings()
                } label: {
                    Label("Save", systemImage: "tray.and.arrow.down.fill")
                }
            }

            Form {
                Picker("Voice", selection: $model.settings.voice) {
                    ForEach(RealtimeVoiceOptions.supportedVoices, id: \.self) { voice in
                        Text(voice).tag(voice)
                    }
                }

                TextField("Music folder", text: $model.settings.musicDirectory)
            }

            if !model.hasAPIKey {
                VStack(alignment: .leading, spacing: 8) {
                    SecureField("OpenAI API key", text: $model.apiKeyInput)
                    Button {
                        model.saveAPIKey()
                    } label: {
                        Label("Save API Key", systemImage: "key.fill")
                    }
                }
            }

            if let error = model.lastError {
                Text(error)
                    .font(.callout)
                    .foregroundStyle(.red)
                    .textSelection(.enabled)
            }

            Spacer()
        }
        .padding(28)
    }
}

struct SettingsView: View {
    @EnvironmentObject private var model: AthenaAppModel

    var body: some View {
        Form {
            Picker("Voice", selection: $model.settings.voice) {
                ForEach(RealtimeVoiceOptions.supportedVoices, id: \.self) { voice in
                    Text(voice).tag(voice)
                }
            }

            TextField("Music folder", text: $model.settings.musicDirectory)

            SecureField("OpenAI API key", text: $model.apiKeyInput)

            HStack {
                Button("Save API Key") {
                    model.saveAPIKey()
                }
                Button("Delete Saved Key", role: .destructive) {
                    model.deleteAPIKey()
                }
            }

            Button("Save Settings") {
                model.saveSettings()
            }
        }
    }
}
