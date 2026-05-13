import Foundation
import Security

public protocol OpenAIKeyStore {
    func readAPIKey() throws -> String?
    func saveAPIKey(_ apiKey: String) throws
    func deleteAPIKey() throws
}

public final class KeychainOpenAIKeyStore: OpenAIKeyStore {
    public static let defaultService = "AthenaCompanion.OpenAI.ApiKey"
    public static let defaultAccount = "OpenAI API Key"

    private let service: String
    private let account: String

    public init(
        service: String = KeychainOpenAIKeyStore.defaultService,
        account: String = KeychainOpenAIKeyStore.defaultAccount
    ) {
        self.service = service
        self.account = account
    }

    public func readAPIKey() throws -> String? {
        var query = baseQuery()
        query[kSecMatchLimit as String] = kSecMatchLimitOne
        query[kSecReturnData as String] = true

        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        if status == errSecItemNotFound {
            return nil
        }

        guard status == errSecSuccess else {
            throw KeychainError.unhandledStatus(status)
        }

        guard let data = result as? Data else {
            throw KeychainError.invalidData
        }

        return String(data: data, encoding: .utf8)
    }

    public func saveAPIKey(_ apiKey: String) throws {
        let trimmed = apiKey.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            throw KeychainError.emptySecret
        }

        let secret = Data(trimmed.utf8)
        var query = baseQuery()
        let update: [String: Any] = [
            kSecValueData as String: secret
        ]

        let updateStatus = SecItemUpdate(query as CFDictionary, update as CFDictionary)
        if updateStatus == errSecSuccess {
            return
        }

        guard updateStatus == errSecItemNotFound else {
            throw KeychainError.unhandledStatus(updateStatus)
        }

        query[kSecValueData as String] = secret
        let addStatus = SecItemAdd(query as CFDictionary, nil)
        guard addStatus == errSecSuccess else {
            throw KeychainError.unhandledStatus(addStatus)
        }
    }

    public func deleteAPIKey() throws {
        let status = SecItemDelete(baseQuery() as CFDictionary)
        if status == errSecSuccess || status == errSecItemNotFound {
            return
        }

        throw KeychainError.unhandledStatus(status)
    }

    private func baseQuery() -> [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
    }
}

public enum KeychainError: Error, Equatable, LocalizedError {
    case emptySecret
    case invalidData
    case unhandledStatus(OSStatus)

    public var errorDescription: String? {
        switch self {
        case .emptySecret:
            return "API key cannot be empty."
        case .invalidData:
            return "The saved API key could not be decoded."
        case .unhandledStatus(let status):
            return "Keychain operation failed with status \(status)."
        }
    }
}

public struct EnvironmentOpenAIKeyStore: OpenAIKeyStore {
    private let key: String

    public init(key: String = "OPENAI_API_KEY") {
        self.key = key
    }

    public func readAPIKey() throws -> String? {
        let value = ProcessInfo.processInfo.environment[key]
        return value?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false ? value : nil
    }

    public func saveAPIKey(_ apiKey: String) throws {
        throw KeychainError.unhandledStatus(errSecUnimplemented)
    }

    public func deleteAPIKey() throws {
        throw KeychainError.unhandledStatus(errSecUnimplemented)
    }
}

public final class CompositeOpenAIKeyStore: OpenAIKeyStore {
    private let environment: OpenAIKeyStore
    private let keychain: OpenAIKeyStore

    public init(
        environment: OpenAIKeyStore = EnvironmentOpenAIKeyStore(),
        keychain: OpenAIKeyStore = KeychainOpenAIKeyStore()
    ) {
        self.environment = environment
        self.keychain = keychain
    }

    public func readAPIKey() throws -> String? {
        if let environmentKey = try environment.readAPIKey() {
            return environmentKey
        }

        return try keychain.readAPIKey()
    }

    public func saveAPIKey(_ apiKey: String) throws {
        try keychain.saveAPIKey(apiKey)
    }

    public func deleteAPIKey() throws {
        try keychain.deleteAPIKey()
    }
}
