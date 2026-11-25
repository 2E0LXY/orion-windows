/*
 * Custom WKPtr implementation replacing <WebKit/WKRetainPtr.h>.
 * Minimal RAII wrapper for WebKit C API reference-counted types.
 */

#pragma once

#include <WebKit/WKType.h>

template<typename T>
class WKPtr {
public:
    WKPtr() : m_ptr(nullptr) {}

    explicit WKPtr(T ptr) : m_ptr(ptr)
    {
        if (m_ptr)
            WKRetain(m_ptr);
    }

    ~WKPtr()
    {
        if (m_ptr)
            WKRelease(m_ptr);
    }

    WKPtr(const WKPtr& o) : m_ptr(o.m_ptr)
    {
        if (m_ptr)
            WKRetain(m_ptr);
    }

    WKPtr(WKPtr&& o) noexcept : m_ptr(o.m_ptr)
    {
        o.m_ptr = nullptr;
    }

    WKPtr& operator=(const WKPtr& o)
    {
        if (this != &o) {
            if (o.m_ptr)
                WKRetain(o.m_ptr);
            if (m_ptr)
                WKRelease(m_ptr);
            m_ptr = o.m_ptr;
        }
        return *this;
    }

    WKPtr& operator=(WKPtr&& o) noexcept
    {
        if (this != &o) {
            if (m_ptr)
                WKRelease(m_ptr);
            m_ptr = o.m_ptr;
            o.m_ptr = nullptr;
        }
        return *this;
    }

    T get() const { return m_ptr; }
    operator T() const { return m_ptr; }

    T leakRef()
    {
        T ptr = m_ptr;
        m_ptr = nullptr;
        return ptr;
    }

private:
    enum AdoptTag { Adopt };
    WKPtr(AdoptTag, T ptr) : m_ptr(ptr) {}

    template<typename U>
    friend WKPtr<U> adoptWK(U);

    T m_ptr;
};

template<typename T>
WKPtr<T> adoptWK(T ptr)
{
    return WKPtr<T>(WKPtr<T>::Adopt, ptr);
}
